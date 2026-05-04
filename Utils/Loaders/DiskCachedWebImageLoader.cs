using System.Security.Cryptography;
using System.Text;
using Avalonia.Media.Imaging;

namespace Pap.erNet.Utils.Loaders;

/// <summary>
///     Provides memory and disk cached way to asynchronously load images for <see cref="ImageLoader" />
///     Can be used as base class if you want to create custom caching mechanism
/// </summary>
public class DiskCachedWebImageLoader : RamCachedWebImageLoader
{
	private readonly string _cacheFolder;

	public DiskCachedWebImageLoader(string cacheFolder = "Cache/Images/")
	{
		_cacheFolder = cacheFolder;
	}

	public DiskCachedWebImageLoader(HttpClient httpClient, bool disposeHttpClient, string cacheFolder = "Cache/Images/")
		: base()
	{
		_cacheFolder = cacheFolder;
	}

	/// <inheritdoc />
	protected override Task<Bitmap?> LoadFromGlobalCache(string url)
	{
		var path = Path.Combine(_cacheFolder, CreateMd5(url));

		if (!File.Exists(path))
		{
			LogHelper.WriteLogAsync($"[DiskCache] 未命中: {url} -> {path}");
			return Task.FromResult<Bitmap?>(null);
		}

		try
		{
			var fileInfo = new FileInfo(path);
			LogHelper.WriteLogAsync(
				$"[DiskCache] 命中: {url} -> {path}, 大小={fileInfo.Length} bytes, 修改时间={fileInfo.LastWriteTime:HH:mm:ss.fff}"
			);

			// 尝试读取文件前几个字节验证是否为有效图片
			using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
			var header = new byte[Math.Min(16, fileInfo.Length)];
			fs.ReadExactly(header, 0, header.Length);
			var headerHex = BitConverter.ToString(header).Replace("-", " ");
			LogHelper.WriteLogAsync($"[DiskCache] 文件头: {headerHex}");

			// 检查常见的图片文件头
			bool isValidImage = IsValidImageHeader(header);
			if (!isValidImage)
			{
				LogHelper.WriteLogAsync($"[DiskCache] 警告: 文件头不像是有效图片，可能文件损坏或不完整");
			}

			fs.Position = 0;
			var bitmap = new Bitmap(fs);
			LogHelper.WriteLogAsync($"[DiskCache] 加载成功: {url}");
			return Task.FromResult<Bitmap?>(bitmap);
		}
		catch (Exception ex)
		{
			LogHelper.WriteLogAsync($"[DiskCache] 加载失败: {url} -> {path}, 异常: {ex.Message}");
			// 如果文件损坏，删除它以便下次重新下载
			try
			{
				File.Delete(path);
			}
			catch
			{
				/* ignore */
			}

			return Task.FromResult<Bitmap?>(null);
		}
	}

	private static bool IsValidImageHeader(byte[] header)
	{
		if (header.Length < 4)
		{
			return false;
		}

		// JPEG: FF D8 FF
		if (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
		{
			return true;
		}

		// PNG: 89 50 4E 47
		if (header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47)
		{
			return true;
		}

		// GIF: 47 49 46 38
		if (header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x38)
		{
			return true;
		}

		// WebP: RIFF....WEBP
		if (
			header.Length >= 12
			&& header[0] == 0x52
			&& header[1] == 0x49
			&& header[2] == 0x46
			&& header[3] == 0x46
			&& header[8] == 0x57
			&& header[9] == 0x45
			&& header[10] == 0x42
			&& header[11] == 0x50
		)
		{
			return true;
		}

		// AVIF/HEIF: ftyp 出现在第4字节开始
		if (header.Length >= 8 && header[4] == 0x66 && header[5] == 0x74 && header[6] == 0x79 && header[7] == 0x70)
		{
			return true;
		}

		return false;
	}

	protected override Task SaveToGlobalCache(string url, byte[] imageBytes)
	{
		var path = Path.Combine(_cacheFolder, CreateMd5(url));
		var tempPath = path + ".tmp";
		try
		{
			Directory.CreateDirectory(_cacheFolder);
			// 先写入临时文件，然后原子重命名，避免半下载问题
			File.WriteAllBytes(tempPath, imageBytes);
			File.Move(tempPath, path, overwrite: true);
			LogHelper.WriteLogAsync($"[DiskCache] 保存成功: {url} -> {path}, 大小={imageBytes.Length} bytes");
		}
		catch (Exception ex)
		{
			LogHelper.WriteLogAsync($"[DiskCache] 保存失败: {url} -> {path}, 异常: {ex.Message}");
			try
			{
				File.Delete(tempPath);
			}
			catch
			{
				/* ignore */
			}
		}

		return Task.CompletedTask;
	}

	private static string CreateMd5(string input)
	{
		// Use input string to calculate MD5 hash
		var inputBytes = Encoding.ASCII.GetBytes(input);
		var hashBytes = MD5.HashData(inputBytes);

		// Convert the byte array to hexadecimal string
		return Convert.ToHexString(hashBytes);
	}
}
