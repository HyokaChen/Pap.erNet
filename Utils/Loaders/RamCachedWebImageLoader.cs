using System.Collections.Concurrent;
using Avalonia.Media.Imaging;

namespace Pap.erNet.Utils.Loaders;

/// <summary>
///     Provides memory cached way to asynchronously load images for <see cref="ImageLoader" />
///     Can be used as base class if you want to create custom in memory caching
/// </summary>
public class RamCachedWebImageLoader : BaseWebImageLoader
{
	// 有界 LRU：避免解码后的 Bitmap 无限累积（壁纸滚动列表场景内存会持续增长）
	private readonly BitmapLruCache _memoryCache = new(maxEntries: 24, maxBytes: 64L * 1024 * 1024);

	// 在途加载去重：同一 URL 并发请求只发起一次网络/解码
	private readonly ConcurrentDictionary<string, Task<Bitmap?>> _inflight = new();

	/// <inheritdoc />
	public RamCachedWebImageLoader()
		: base() { }

	/// <inheritdoc />
	public RamCachedWebImageLoader(IHttpClientFactory httpClientFactory)
		: base(httpClientFactory) { }

	/// <inheritdoc />
	public override async Task<Bitmap?> ProvideImageAsync(string url, CancellationToken cancellationToken = default)
	{
		// Null check to prevent ArgumentNullException in ConcurrentDictionary.GetOrAdd
		if (string.IsNullOrEmpty(url))
			return null;

		// 检查是否已在内存缓存中
		if (_memoryCache.TryGet(url, out var cached))
		{
			LogHelper.WriteLogAsync($"[MemoryCache] 命中: {url}");
			return cached;
		}

		LogHelper.WriteLogAsync($"[MemoryCache] 未命中，开始加载: {url}");
		var bitmapTask = _inflight.GetOrAdd(url, _ => LoadAsync(url, cancellationToken));
		var bitmap = await bitmapTask.ConfigureAwait(false);

		// 完成后立即移除在途条目（仅移除自己）：
		// 1) 失败时允许下次重试；2) 避免 Task.Result 强引用位图导致 LRU 淘汰失效
		if (_inflight.TryGetValue(url, out var current) && ReferenceEquals(current, bitmapTask))
		{
			_inflight.TryRemove(url, out _);
		}

		// If load failed - do not cache, next load attempt will try to load image again
		if (bitmap != null)
		{
			_memoryCache.Add(url, bitmap);
		}

		return bitmap;
	}
}
