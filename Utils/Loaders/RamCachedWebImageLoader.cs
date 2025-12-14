using System.Collections.Concurrent;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;

namespace Pap.erNet.Utils.Loaders;

/// <summary>
///     Provides memory cached way to asynchronously load images for <see cref="ImageLoader" />
///     Can be used as base class if you want to create custom in memory caching
/// </summary>
public class RamCachedWebImageLoader : BaseWebImageLoader
{
	private readonly ConcurrentDictionary<string, Task<Bitmap?>> _memoryCache = new();

	/// <inheritdoc />
	public RamCachedWebImageLoader()
		: base() { }

	/// <inheritdoc />
	public override async Task<Bitmap?> ProvideImageAsync(string url)
	{
		// Null check to prevent ArgumentNullException in ConcurrentDictionary.GetOrAdd
		if (string.IsNullOrEmpty(url))
			return null;

		var bitmap = await _memoryCache.GetOrAdd(url, LoadAsync).ConfigureAwait(false);
		// If load failed - remove from cache and return
		// Next load attempt will try to load image again
		if (bitmap == null)
			_memoryCache.TryRemove(url, out _);
		return bitmap;
	}
}
