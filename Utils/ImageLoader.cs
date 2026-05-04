using System.Collections.Concurrent;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Pap.erNet.Utils.Loaders;

namespace Pap.erNet.Utils;

public class ImageLoader
{
	public const string AsyncImageLoaderLogArea = "AsyncImageLoader";

	public static readonly AttachedProperty<string?> SourceProperty = AvaloniaProperty.RegisterAttached<Image, string?>(
		"Source",
		typeof(ImageLoader)
	);

	public static readonly AttachedProperty<string?> ThumbnailProperty = AvaloniaProperty.RegisterAttached<Image, string?>(
		"Thumbnail",
		typeof(ImageLoader)
	);

	public static readonly AttachedProperty<bool> LoadStatusProperty = AvaloniaProperty.RegisterAttached<Image, bool>(
		"LoadStatus",
		typeof(ImageLoader)
	);

	static ImageLoader()
	{
		SourceProperty.Changed.AddClassHandler<Image>(OnSourceChanged);
		LoadStatusProperty.Changed.AddClassHandler<Image>(OnLoadStatusChanged);
	}

	private static readonly string TempFolder = Path.Combine(Path.GetTempPath(), "Pap.erNet");

	private static DiskCachedWebImageLoader AsyncImageLoader { get; set; } = new(TempFolder);

	private static ConcurrentDictionary<Image, CancellationTokenSource> _pendingOperations = new();

	private static async void OnLoadStatusChanged(Image sender, AvaloniaPropertyChangedEventArgs args)
	{
		try
		{
			var loadStatus = args.GetNewValue<bool>();
			var url = GetSource(sender);

			LogHelper.WriteLogAsync($"OnLoadStatusChanged: loadStatus={loadStatus}, url={(string.IsNullOrEmpty(url) ? "(null)" : url)}");

			// Cancel/Add new pending operation
			var cts = _pendingOperations.AddOrUpdate(
				sender,
				new CancellationTokenSource(),
				(_, y) =>
				{
					y.Cancel();
					return new CancellationTokenSource();
				}
			);

			if (!loadStatus)
			{
				// 图片离开视窗，取消加载并恢复缩略图
				_pendingOperations.TryRemove(new KeyValuePair<Image, CancellationTokenSource>(sender, cts));
				RestoreThumbnail(sender);
				return;
			}

			if (string.IsNullOrEmpty(url))
			{
				_pendingOperations.TryRemove(new KeyValuePair<Image, CancellationTokenSource>(sender, cts));
				return;
			}

			// 加载图片，如果失败且仍在视窗内则自动重试
			await LoadWithRetryAsync(sender, url!, cts);
		}
		catch (Exception e)
		{
			throw; // TODO 处理异常
		}
	}

	/// <summary>
	///     在视窗区域内反复重试加载图片，直到成功或离开视窗。
	///     每次失败后延迟递增时间再重试，避免频繁请求。
	/// </summary>
	private static async Task LoadWithRetryAsync(Image sender, string url, CancellationTokenSource cts)
	{
		const int initialRetryDelayMs = 2000; // 初始重试延迟 2 秒
		const int maxRetryDelayMs = 30000; // 最大重试延迟 30 秒
		var currentDelayMs = initialRetryDelayMs;

		while (!cts.Token.IsCancellationRequested)
		{
			var bitmap = await Task.Run(
					async () =>
					{
						try
						{
							// A small delay allows to cancel early if the image goes out of screen too fast (eg. scrolling)
							// The Bitmap constructor is expensive and cannot be cancelled
							await Task.Delay(20, cts.Token);

							return await AsyncImageLoader.ProvideImageAsync(url);
						}
						catch (TaskCanceledException)
						{
							return null;
						}
						catch (Exception ex)
						{
							LogHelper.WriteLogAsync(ex.StackTrace);
							return null;
						}
					},
					cts.Token
				)
				.ConfigureAwait(true);

			if (cts.Token.IsCancellationRequested)
				break;

			if (bitmap != null)
			{
				sender.Source = bitmap;
				break;
			}

			// 加载失败，检查是否仍在视窗区域内（LoadStatus 为 true）
			if (!GetLoadStatus(sender))
				break;

			// 等待递增延迟后重试
			try
			{
				await Task.Delay(currentDelayMs, cts.Token);
			}
			catch (TaskCanceledException)
			{
				break;
			}

			// 递增延迟，但不超过最大值
			currentDelayMs = Math.Min(currentDelayMs * 2, maxRetryDelayMs);
		}

		// "It is not guaranteed to be thread safe by ICollection, but ConcurrentDictionary's implementation is. Additionally, we recently exposed this API for .NET 5 as a public ConcurrentDictionary.TryRemove"
		_pendingOperations.TryRemove(new KeyValuePair<Image, CancellationTokenSource>(sender, cts));
	}

	private static void OnSourceChanged(Image sender, AvaloniaPropertyChangedEventArgs args)
	{
		// Source 变化时，如果 LoadStatus 为 true（在视窗内），直接加载新图片
		// 否则显示缩略图
		if (GetLoadStatus(sender))
		{
			// 在视窗内，LoadStatus 属性变化会触发 OnLoadStatusChanged
			// 这里需要手动触发一次加载
			var url = args.GetNewValue<string?>();
			if (!string.IsNullOrEmpty(url))
			{
				var cts = _pendingOperations.AddOrUpdate(
					sender,
					new CancellationTokenSource(),
					(_, y) =>
					{
						y.Cancel();
						return new CancellationTokenSource();
					}
				);

				// 不 await，fire-and-forget 加载
				_ = LoadWithRetryAsync(sender, url!, cts);
			}
		}
		else
		{
			// 不在视窗内，显示缩略图
			RestoreThumbnail(sender);
		}
	}

	/// <summary>
	///     恢复显示缩略图。当图片离开视窗或加载被取消时调用。
	/// </summary>
	private static void RestoreThumbnail(Image sender)
	{
		var thumbnail = GetThumbnail(sender);
		if (!string.IsNullOrEmpty(thumbnail))
		{
			try
			{
				var arr = Convert.FromBase64String(thumbnail.Replace("data:image/webp;base64,", ""));
				using var ms = new MemoryStream(arr);
				sender.Source = new Bitmap(ms);
			}
			catch (Exception)
			{
				// 缩略图解码失败，忽略
			}
		}
	}

	public static string? GetSource(Image element)
	{
		return element.GetValue(SourceProperty);
	}

	public static void SetSource(Image element, string? value)
	{
		element.SetValue(SourceProperty, value);
	}

	public static void SetThumbnail(Image element, string? value)
	{
		element.SetValue(ThumbnailProperty, value);
	}

	public static string? GetThumbnail(Image element)
	{
		return element.GetValue(ThumbnailProperty);
	}

	public static bool GetLoadStatus(Image element)
	{
		return element.GetValue(LoadStatusProperty);
	}

	public static void SetLoadStatus(Image element, bool value)
	{
		element.SetValue(LoadStatusProperty, value);
	}
}
