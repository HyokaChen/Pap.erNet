using Avalonia.Media.Imaging;

namespace Pap.erNet.Utils.Loaders;

/// <summary>
/// 按数量与估算字节数双重上限的 LRU 位图缓存（线程安全）。
/// 淘汰时不主动 Dispose：位图可能仍被 UI 引用，生命周期交给 GC 与引用方。
/// </summary>
internal sealed class BitmapLruCache
{
	private readonly int _maxEntries;
	private readonly long _maxBytes;
	private readonly object _lock = new();
	private readonly Dictionary<string, LinkedListNode<CacheEntry>> _map = new();
	private readonly LinkedList<CacheEntry> _lru = new(); // Head = 最近使用
	private long _totalBytes;

	public BitmapLruCache(int maxEntries, long maxBytes)
	{
		_maxEntries = maxEntries;
		_maxBytes = maxBytes;
	}

	public bool TryGet(string key, out Bitmap? bitmap)
	{
		lock (_lock)
		{
			if (_map.TryGetValue(key, out var node))
			{
				_lru.Remove(node);
				_lru.AddFirst(node);
				bitmap = node.Value.Bitmap;
				return true;
			}
		}

		bitmap = null;
		return false;
	}

	public void Add(string key, Bitmap bitmap)
	{
		var entry = new CacheEntry(key, bitmap, EstimateBytes(bitmap));
		lock (_lock)
		{
			// 同键更新：替换值并提前
			if (_map.TryGetValue(key, out var existing))
			{
				_totalBytes -= existing.Value.EstimatedBytes;
				existing.Value = entry;
				_lru.Remove(existing);
				_lru.AddFirst(existing);
				_totalBytes += entry.EstimatedBytes;
				return;
			}

			var node = _lru.AddFirst(entry);
			_map[key] = node;
			_totalBytes += entry.EstimatedBytes;
			Evict();
		}
	}

	public void Remove(string key)
	{
		lock (_lock)
		{
			if (_map.Remove(key, out var node))
			{
				_lru.Remove(node);
				_totalBytes -= node.Value.EstimatedBytes;
			}
		}
	}

	public void Clear()
	{
		lock (_lock)
		{
			_map.Clear();
			_lru.Clear();
			_totalBytes = 0;
		}
	}

	private void Evict()
	{
		while (_lru.Count > _maxEntries || _totalBytes > _maxBytes)
		{
			var tail = _lru.Last;
			if (tail == null)
				return;
			_lru.RemoveLast();
			_map.Remove(tail.Value.Key);
			_totalBytes -= tail.Value.EstimatedBytes;
		}
	}

	private static long EstimateBytes(Bitmap bitmap)
	{
		var size = bitmap.PixelSize;
		return (long)size.Width * size.Height * 4L; // BGRA
	}

	private sealed class CacheEntry
	{
		public CacheEntry(string key, Bitmap bitmap, long estimatedBytes)
		{
			Key = key;
			Bitmap = bitmap;
			EstimatedBytes = estimatedBytes;
		}

		public string Key { get; }
		public Bitmap Bitmap { get; }
		public long EstimatedBytes { get; }
	}
}
