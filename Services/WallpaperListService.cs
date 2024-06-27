using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Blurhash.ImageSharp;
using GraphQL;
using Pap.erNet.Models;
using Pap.erNet.Utils;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Pap.erNet.Services;

public class WallpaperListService
{
    public async IAsyncEnumerable<Wallpaper> DiscoverItemsAsync()
    {
        var photosQueryRequest = new GraphQLRequest
        {
            Query = RequestUtil.GraphQLQuery,
            OperationName = "Photos",
            Variables = new
            {
                listId = "2244936390884196352",
                after = (string)null,
                before = (string)null,
                filters = new { }
            }
        };
        var graphQLResponse = await RequestUtil.GraphQLClient.SendQueryAsync<ResponseType>(
            photosQueryRequest
        );
        var after = graphQLResponse.Data.Photos.After;
        var before = graphQLResponse.Data.Photos.Before;
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
            var thumbnail = image.ToBase64String(
                SixLabors.ImageSharp.Formats.Webp.WebpFormat.Instance
            );
            yield return new Wallpaper()
            {
                Id = entry.Id,
                Url = entry.Urls.Thumb,
                Link = entry.Link,
                Author = entry.Heading,
                Thumbnail = thumbnail,
                ResolutionRatio = ComputeResolutionRatio(entry.Width, entry.Height)
            };
        }
    }

    internal static string ComputeResolutionRatio(int width, int height)
    {
        var result = string.Empty;
        if (width > 7680 && height > 4320)
        {
            result = "8K";
        }
        else if (width > 5120 && height > 2880)
        {
            result = "5K";
        }
        else if ((width > 4096 && height > 2160) || (width > 3840 && height > 2160))
        {
            result = "4K";
        }
        else if (width > 2560 && height > 1440)
        {
            result = "2K";
        }
        return result;
    }

    public async IAsyncEnumerable<Wallpaper> LatestItemsAsync()
    {
        var photosQueryRequest = new GraphQLRequest
        {
            Query = RequestUtil.GraphQLQuery,
            OperationName = "Photos",
            Variables = new
            {
                listId = "2416408299759992832",
                after = (string)null,
                before = (string)null,
                filters = new { }
            }
        };
        var graphQLResponse = await RequestUtil.GraphQLClient.SendQueryAsync<ResponseType>(
            photosQueryRequest
        );
        var after = graphQLResponse.Data.Photos.After;
        var before = graphQLResponse.Data.Photos.Before;
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
            var thumbnail = image.ToBase64String(
                SixLabors.ImageSharp.Formats.Webp.WebpFormat.Instance
            );
            yield return new Wallpaper()
            {
                Id = entry.Id,
                Url = entry.Urls.Thumb,
                Link = entry.Link,
                Author = entry.Heading,
                Thumbnail = thumbnail,
                ResolutionRatio = ComputeResolutionRatio(entry.Width, entry.Height)
            };
        }
    }

    public async IAsyncEnumerable<Wallpaper> VerticalScreenItemsAsync()
    {
        var photosQueryRequest = new GraphQLRequest
        {
            Query = RequestUtil.GraphQLQuery,
            OperationName = "Photos",
            Variables = new
            {
                listId = "2245081321414066176",
                after = (string)null,
                before = (string)null,
                filters = new { }
            }
        };
        var graphQLResponse = await RequestUtil.GraphQLClient.SendQueryAsync<ResponseType>(
            photosQueryRequest
        );
        var after = graphQLResponse.Data.Photos.After;
        var before = graphQLResponse.Data.Photos.Before;
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
            var thumbnail = image.ToBase64String(
                SixLabors.ImageSharp.Formats.Webp.WebpFormat.Instance
            );
            yield return new Wallpaper()
            {
                Id = entry.Id,
                Url = entry.Urls.Thumb,
                Link = entry.Link,
                Author = entry.Heading,
                Thumbnail = thumbnail,
                ResolutionRatio = ComputeResolutionRatio(entry.Width, entry.Height)
            };
        }
    }
}
