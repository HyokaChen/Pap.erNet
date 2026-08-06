using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Blurhash.ImageSharp;
using Pap.erNet.Models;
using Pap.erNet.Utils;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;

namespace Pap.erNet.Services;

public class WallpaperListService
{
	// 按分类缓存的壁纸列表（含 blurhash 缩略图），切换 Tab 时立即显示占位，避免黑屏空窗
	private static readonly ConcurrentDictionary<string, List<Wallpaper>> CachedLists = new();

	// 每分类缓存上限：视口内仅需数条，100 条足够覆盖快速滚动；防止 ∞ 滚动后内存膨胀
	private const int MaxCachedPerList = 100;

	/// <summary>
	/// 获取指定分类最近一次加载的壁纸列表（用于切换时秒显 blurhash 占位）
	/// </summary>
	public static bool TryGetCachedList(string listId, out List<Wallpaper>? list) => CachedLists.TryGetValue(listId, out list);

	/// <summary>
	/// 根据 listId 获取壁纸列表（通用方法，替代原来的三个独立方法）
	/// 按 after 游标逐页拉取：消费端逐条 yield，拉完一页自动请求下一页，背压天然生效
	/// </summary>
	public static async IAsyncEnumerable<Wallpaper> GetWallpapersAsync(
		string listId,
		[EnumeratorCancellation] CancellationToken cancellationToken = default
	)
	{
		// 预解析 CDN 域名（首次调用时会请求 status 接口）
		await CdnResolver.GetResolvedCdnHostAsync().ConfigureAwait(false);

		var page = new List<Wallpaper>();
		try
		{
			string? after = null;
			while (true)
			{
				var graphQlResponse = await RequestUtil
					.GetResponse(listId, after: after, cancellationToken: cancellationToken)
					.ConfigureAwait(false);
				if (graphQlResponse?.Data?.Photos?.Entries is not { Count: > 0 } entries)
				{
					LogHelper.WriteLogAsync($"GetWallpapersAsync {listId}: 响应为空或没有更多数据，结束");
					yield break;
				}

				after = graphQlResponse.Data.Photos.After;
				Debug.WriteLine($"GetWallpapersAsync {listId}, after:${after}");
				LogHelper.WriteLogAsync($"GetWallpapersAsync {listId}: 本页 {entries.Count} 条, 下一页游标={after ?? "(null)"}");

				foreach (var entry in entries)
				{
					var thumbUrl = entry.Urls.Thumb.Replace("http://", "https://");
					var resolvedUrl = CdnResolver.ResolveCdnUrl(thumbUrl);
					var res = new Wallpaper
					{
						Id = entry.Id,
						Url = resolvedUrl,
						Link = entry.Link,
						Author = entry.Heading,
						// 缩略图不在此处同步编码（每条 blurhash→WebP 编码约 150-200ms，
						// 10 条会把首屏阻塞 1-2 秒）；由 ViewModel 后台异步编码后填充。
						// Color 是即时可用的占位色，切换瞬间先显示色块，避免空白闪烁。
						Blurhash = entry.Blurhash,
						Color = entry.Color,
						Thumbnail = string.Empty,
						ResolutionRatio = ComputeResolutionRatio(entry.Width, entry.Height),
					};
					LogHelper.WriteLogAsync($"返回的结果:{res.Author} >>>>> {res.Url}");
					page.Add(res);
					yield return res;
				}

				// 没有下一页游标，结束
				if (string.IsNullOrEmpty(after))
					yield break;
			}
		}
		finally
		{
			// 迭代结束（含中途取消/切换）后缓存本次已拉取的数据，供下次切换秒显
			if (page.Count > 0)
			{
				if (page.Count > MaxCachedPerList)
					page = page.GetRange(0, MaxCachedPerList);
				CachedLists[listId] = page;
			}
		}
	}

	// blurhash -> WebP base64 编码结果缓存：同一条壁纸跨列表重载/跨 VM 实例复用，避免重复编码
	private static readonly ConcurrentDictionary<string, Task<string>> ThumbnailEncodings = new();

	// 编码结果磁盘缓存目录（跨会话）：第二次起 blurhash 即时可用，先于真实图显示
	private static readonly string BlurhashCacheFolder = Path.Combine(Path.GetTempPath(), "Pap.erNet", "blurhash");

	/// <summary>
	/// 异步编码 blurhash 为 560x320 WebP base64 缩略图（内存缓存 + 磁盘缓存 + 后台线程）。
	/// </summary>
	public static async Task<string> EncodeThumbnailAsync(string blurhash)
	{
		if (string.IsNullOrEmpty(blurhash))
			return string.Empty;

		// 内存缓存（会话内）
		if (ThumbnailEncodings.TryGetValue(blurhash, out var cached))
			return await cached.ConfigureAwait(false);

		// 磁盘缓存（跨会话）：blurhash 确定性编码，命中即免编码
		var cachePath = Path.Combine(BlurhashCacheFolder, CreateMd5(blurhash) + ".b64");
		if (File.Exists(cachePath))
		{
			try
			{
				var fromDisk = await File.ReadAllTextAsync(cachePath).ConfigureAwait(false);
				if (!string.IsNullOrEmpty(fromDisk))
				{
					ThumbnailEncodings.TryAdd(blurhash, Task.FromResult(fromDisk));
					return fromDisk;
				}
			}
			catch
			{
				// 缓存损坏则忽略，重新编码
			}
		}

		var task = Task.Run(() => EncodeBlurhash(blurhash));
		var existing = ThumbnailEncodings.GetOrAdd(blurhash, task);
		var result = await existing.ConfigureAwait(false);

		// 编码失败不缓存，允许下次重试
		if (string.IsNullOrEmpty(result))
		{
			ThumbnailEncodings.TryRemove(blurhash, out _);
		}
		else
		{
			SaveBlurhashToDisk(cachePath, result);
		}

		return result;
	}

	/// <summary>
	/// 编码结果异步落盘（临时文件 + 原子替换，避免并发写冲突）
	/// </summary>
	private static async void SaveBlurhashToDisk(string path, string content)
	{
		try
		{
			Directory.CreateDirectory(BlurhashCacheFolder);
			var tempPath = path + ".tmp";
			await File.WriteAllTextAsync(tempPath, content).ConfigureAwait(false);
			File.Move(tempPath, path, overwrite: true);
		}
		catch
		{
			// 写缓存失败不影响功能
		}
	}

	private static string CreateMd5(string input)
	{
		var inputBytes = System.Text.Encoding.ASCII.GetBytes(input);
		var hashBytes = System.Security.Cryptography.MD5.HashData(inputBytes);
		return Convert.ToHexString(hashBytes);
	}

	/// <summary>
	/// 由 blurhash 解码生成 560x320 WebP base64 缩略图（CPU 密集，须在后台线程执行）
	/// </summary>
	private static string EncodeBlurhash(string blurhash)
	{
		try
		{
			using var image = Blurhasher.Decode(blurhash, 560, 320);
			return image.ToBase64String(WebpFormat.Instance);
		}
		catch (Exception ex)
		{
			LogHelper.WriteLogAsync($"BlurHash 解码失败: {ex.Message}");
			return string.Empty;
		}
	}

	private static string ComputeResolutionRatio(int width, int height)
	{
		var result = string.Empty;
		switch (width)
		{
			case >= 7680 when height >= 4320:
				result = "8K";
				break;
			case >= 5120 when height >= 2880:
				result = "5K";
				break;
			case >= 4096 when height >= 2160:
			case >= 3840 when height >= 2160:
				result = "4K";
				break;
			case >= 2560 when height >= 1440:
				result = "2K";
				break;
		}
		return result;
	}
}
