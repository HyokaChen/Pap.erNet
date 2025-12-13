using System.Diagnostics;
using Blurhash.ImageSharp;
using Pap.erNet.Models;
using Pap.erNet.Utils;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Pap.erNet.Services;

public class WallpaperListService
{
	public async IAsyncEnumerable<Wallpaper> DiscoverItemsAsync()
	{
		var graphQLResponse = await RequestUtil.GetResponse("2244936390884196352");

		var after = graphQLResponse!.Data.Photos.After;
		var before = graphQLResponse.Data.Photos.Before;
		Debug.WriteLine($"DiscoverItemsAsync 2244936390884196352, after:${after}, before: ${before}");
		var entries = graphQLResponse.Data.Photos.Entries;
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
			var res = new Wallpaper()
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

	internal static string ComputeResolutionRatio(int width, int height)
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

	public async IAsyncEnumerable<Wallpaper> LatestItemsAsync()
	{
		var graphQLResponse = await RequestUtil.GetResponse("2416408299759992832");

		var after = graphQLResponse!.Data.Photos.After;
		var before = graphQLResponse.Data.Photos.Before;
		Debug.WriteLine($"LatestItemsAsync 2416408299759992832, after:${after}, before: ${before}");
		var entries = graphQLResponse.Data.Photos.Entries;
		foreach (var entry in entries)
		{
			if (entry.Blurhash == null)
			{
				Debug.WriteLine($"BlurHash(${entry.Id}) == null:::{entry.Urls.Thumb}");
				var sourceImage = await Image.LoadAsync<Rgba32>(entry.Urls.Thumb);
				entry.Blurhash = Blurhasher.Encode(sourceImage, 4, 3);
			}
			var image = Blurhasher.Decode(entry.Blurhash, 560, 320);
			var thumbnail = image.ToBase64String(SixLabors.ImageSharp.Formats.Webp.WebpFormat.Instance);
			yield return new Wallpaper()
			{
				Id = entry.Id,
				Url = entry.Urls.Thumb.Replace("http://", "https://"),
				Link = entry.Link,
				Author = entry.Heading,
				Thumbnail = thumbnail,
				ResolutionRatio = ComputeResolutionRatio(entry.Width, entry.Height),
			};
		}
	}

	public async IAsyncEnumerable<Wallpaper> VerticalScreenItemsAsync()
	{
		var graphQLResponse = await RequestUtil.GetResponse("2245081321414066176");

		var after = graphQLResponse.Data.Photos.After;
		var before = graphQLResponse.Data.Photos.Before;
		Debug.WriteLine($"VerticalScreenItemsAsync 2245081321414066176, after:${after}, before: ${before}");
		var entries = graphQLResponse.Data.Photos.Entries;
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
			yield return new Wallpaper()
			{
				Id = entry.Id,
				Url = entry.Urls.Thumb.Replace("http://", "https://"),
				Link = entry.Link,
				Author = entry.Heading,
				Thumbnail = thumbnail,
				ResolutionRatio = ComputeResolutionRatio(entry.Width, entry.Height),
			};
		}
	}
}
