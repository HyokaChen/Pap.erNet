using System.Diagnostics;
using Blurhash.ImageSharp;
using Pap.erNet.Models;
using Pap.erNet.Utils;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Pap.erNet.Services;

public class WallpaperListService
{
	/// <summary>
	/// 根据 listId 获取壁纸列表（通用方法，替代原来的三个独立方法）
	/// </summary>
	public async IAsyncEnumerable<Wallpaper> GetWallpapersAsync(string listId)
	{
		var graphQlResponse = await RequestUtil.GetResponse(listId);

		var after = graphQlResponse!.Data.Photos.After;
		var before = graphQlResponse.Data.Photos.Before;
		Debug.WriteLine($"GetWallpapersAsync {listId}, after:${after}, before: ${before}");
		var entries = graphQlResponse.Data.Photos.Entries;
		foreach (var entry in entries)
		{
			if (entry.Blurhash == null)
			{
				var sourceImage = await Image.LoadAsync<Rgba32>(entry.Urls.Thumb);
				entry.Blurhash = Blurhasher.Encode(sourceImage, 4, 3);
				Debug.WriteLine($"BlurHash(${entry.Id}) == null:::{entry.Urls.Thumb}");
			}
			var image = Blurhasher.Decode(entry.Blurhash, 560, 320);
			var thumbnail = image.ToBase64String(SixLabors.ImageSharp.Formats.Webp.WebpFormat.Instance);
			var res = new Wallpaper
			{
				Id = entry.Id,
				Url = entry.Urls.Thumb.Replace("http://", "https://"),
				Link = entry.Link,
				Author = entry.Heading,
				Thumbnail = thumbnail,
				ResolutionRatio = ComputeResolutionRatio(entry.Width, entry.Height),
			};
			LogHelper.WriteLogAsync($"返回的结果:{res.Author} >>>>> {res.Url}");
			yield return res;
		}
	}

	private static string ComputeResolutionRatio(int width, int height)
	{
		var result = string.Empty;
		switch (width)
		{
			case > 7680 when height > 4320:
				result = "8K";
				break;
			case > 5120 when height > 2880:
				result = "5K";
				break;
			case > 4096 when height > 2160:
			case > 3840 when height > 2160:
				result = "4K";
				break;
			case > 2560 when height > 1440:
				result = "2K";
				break;
		}
		return result;
	}
}
