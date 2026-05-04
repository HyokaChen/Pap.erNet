using System.Collections.Concurrent;
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
	public RamCachedWebImageLoader(IHttpClientFactory httpClientFactory)
		: base(httpClientFactory) { }

	/// <inheritdoc />
	public override async Task<Bitmap?> ProvideImageAsync(string url)
	{
		// Null check to prevent ArgumentNullException in ConcurrentDictionary.GetOrAdd
		if (string.IsNullOrEmpty(url))
			return null;

		// 检查是否已在内存缓存中
		if (_memoryCache.TryGetValue(url, out var existingTask))
		{
			LogHelper.WriteLogAsync($"[MemoryCache] 命中: {url}");
			var bitmap = await existingTask.ConfigureAwait(false);
			if (bitmap == null)
				_memoryCache.TryRemove(url, out _);
			return bitmap;
		}

		LogHelper.WriteLogAsync($"[MemoryCache] 未命中，开始加载: {url}");
		var bitmapTask = _memoryCache.GetOrAdd(url, _ => LoadAsync(url));
		var bitmap2 = await bitmapTask.ConfigureAwait(false);
		// If load failed - remove from cache and return
		// Next load attempt will try to load image again
		if (bitmap2 == null)
			_memoryCache.TryRemove(url, out _);
		return bitmap2;
	}
}
