using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Pap.erNet.Utils.Loaders;

namespace Pap.erNet.Utils;

public class ImageLoader
{
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
		ThumbnailProperty.Changed.AddClassHandler<Image>(OnThumbnailChanged);
		LoadStatusProperty.Changed.AddClassHandler<Image>(OnLoadStatusChanged);
	}

	private static readonly string TempFolder = Path.Combine(Path.GetTempPath(), "Pap.erNet");

	private static DiskCachedWebImageLoader? _asyncImageLoader;

	private static DiskCachedWebImageLoader AsyncImageLoader => _asyncImageLoader ??= CreateImageLoader();

	private static DiskCachedWebImageLoader CreateImageLoader()
	{
		var app = Application.Current as App;
		var factory = app?.ServicesProvider.GetService(typeof(IHttpClientFactory)) as IHttpClientFactory;
		return factory != null ? new DiskCachedWebImageLoader(factory, TempFolder) : new DiskCachedWebImageLoader(TempFolder);
	}

	private static readonly ConcurrentDictionary<Image, CancellationTokenSource> PendingOperations = new();

	// 当前 Image 上显示的位图对应的 URL 与类型（占位/真实图）。
	// 容器回收复用时 Source 会残留上一个 Tab 的真实图，需要据此识别并清除，
	// 否则 blurhash 占位被"Source 非空"守卫跳过，切换时直接跳变闪烁。
	// 弱引用：控件销毁后自动清理，不泄漏。
	private static readonly ConditionalWeakTable<Image, ShownState> ShownUrls = new();

	private sealed class ShownState
	{
		public required string Url { get; init; }
		public required bool IsReal { get; init; }
	}

	// 缩略图（blurhash WebP base64）解码结果缓存：滚动回看时避免重复解码；
	// 560x320 位图约 0.7MB，32 张上限 ≈ 22MB，滚动热窗口足够
	private static readonly BitmapLruCache ThumbnailCache = new(maxEntries: 32, maxBytes: 32L * 1024 * 1024);

	/// <summary>
	/// 当缩略图数据变化时触发
	/// 如果当前在视窗内且没有高清图，立即显示缩略图
	/// </summary>
	private static void OnThumbnailChanged(Image sender, AvaloniaPropertyChangedEventArgs args)
	{
		var thumbnail = args.GetNewValue<string?>();
		if (string.IsNullOrEmpty(thumbnail))
			return;

		// 如果当前在视窗内，立即显示缩略图
		if (GetLoadStatus(sender))
		{
			LogHelper.WriteLogAsync("OnThumbnailChanged: 缩略图数据到达，立即显示");
			ShowThumbnail(sender, thumbnail);
		}
	}

	private static void OnLoadStatusChanged(Image sender, AvaloniaPropertyChangedEventArgs args)
	{
		var loadStatus = args.GetNewValue<bool>();
		var url = GetSource(sender);

		LogHelper.WriteLogAsync($"OnLoadStatusChanged: loadStatus={loadStatus}, url={url ?? "(null)"}");

		// 取消之前的加载任务
		CancelAndRemove(sender);

		if (!loadStatus || string.IsNullOrEmpty(url))
		{
			// 离开视窗：保留当前图片（真实图由有界内存缓存管理，无需回退为缩略图释放内存），
			// 滚动回来时直接所见即所得，避免卡在 blurhash 占位上
			return;
		}

		// 容器回收复用：Source 可能残留上一个 Tab/条目的真实图（URL 不匹配）→ 清除，
		// 让当前条目的 blurhash 占位得以显示，切换时平滑过渡而非直接跳变
		if (ShownUrls.TryGetValue(sender, out var shown) && shown.Url != url)
		{
			sender.Source = null;
			ShownUrls.Remove(sender);
		}

		// 进入视窗：先立即显示缩略图（如果已有缩略图数据且当前无图片）
		ShowThumbnail(sender);

		// 然后启动异步加载高清图
		var cts = new CancellationTokenSource();
		PendingOperations[sender] = cts;
		_ = LoadImageAsync(sender, url, cts);
	}

	private static void OnSourceChanged(Image sender, AvaloniaPropertyChangedEventArgs args)
	{
		var url = args.GetNewValue<string?>();
		if (string.IsNullOrEmpty(url))
			return;

		// 如果已经在视窗内，触发加载
		if (GetLoadStatus(sender))
		{
			CancelAndRemove(sender);

			// URL 变化（容器复用/换 Tab）→ Source 残留旧图 → 清除后显示当前条目占位
			if (ShownUrls.TryGetValue(sender, out var shown) && shown.Url != url)
			{
				sender.Source = null;
				ShownUrls.Remove(sender);
			}

			ShowThumbnail(sender);

			var cts = new CancellationTokenSource();
			PendingOperations[sender] = cts;
			_ = LoadImageAsync(sender, url, cts);
		}
	}

	/// <summary>
	/// 异步加载图片，加载完成后更新到 Image.Source
	/// </summary>
	private static async Task LoadImageAsync(Image image, string url, CancellationTokenSource cts)
	{
		var cancellationToken = cts.Token;
		try
		{
			LogHelper.WriteLogAsync($"[图片加载] 开始: {url}");

			// 网络并发限制已下沉到 Loader 的网络层，缓存命中不受限流影响；
			// 解码在 Loader 内强制后台线程执行
			var bitmap = await AsyncImageLoader.ProvideImageAsync(url, cancellationToken).ConfigureAwait(false);

			if (cancellationToken.IsCancellationRequested)
			{
				LogHelper.WriteLogAsync($"[图片加载] 取消(加载完成后): {url}");
				return;
			}

			if (bitmap != null)
			{
				// 两阶段过渡：先确保占位（blurhash/色块）显示，再延迟替换真实图——
				// blurhash 异步编码（首次 ~200ms）可能晚于磁盘缓存真实图（~50ms），
				// 不等待的话占位被"Source 非空"守卫跳过，用户只见色块不见 blurhash。
				var isRealShown = ShownUrls.TryGetValue(image, out var state) && state.Url == url && state.IsReal;
				if (!isRealShown)
				{
					// 占位（blurhash/色块）至少停留 300ms，保证"先模糊后清晰"过渡可感知——
					// 缓存命中时 blurhash 即时显示，若停留太短（如 120ms）视觉上仍是快速跳变
					const int MinPlaceholderHoldMs = 300;

					// 等待占位显示（blurhash 编码完成触发 OnThumbnailChanged 设置 Source）。
					// 轮询 ShownUrls 而非 UI 属性：纯 .NET 状态，后台线程读取安全。
					var shownAt = Environment.TickCount64;
					var waitUntil = shownAt + 250;
					while (Environment.TickCount64 < waitUntil && !cancellationToken.IsCancellationRequested)
					{
						if (ShownUrls.TryGetValue(image, out var s) && s.Url == url)
						{
							shownAt = Environment.TickCount64; // blurhash 已显示，记录时刻
							break;
						}
						await Task.Delay(30, cancellationToken).ConfigureAwait(false);
					}

					// 轮询期间 blurhash 就绪但事件错过：UI 线程补一次占位显示
					await Dispatcher.UIThread.InvokeAsync(() =>
					{
						if (image.Source is null && GetLoadStatus(image) && !cancellationToken.IsCancellationRequested)
						{
							ShowThumbnail(image);
							shownAt = Environment.TickCount64;
						}
					});

					// 从占位实际显示时刻起算，补足最短停留时间
					var elapsed = Environment.TickCount64 - shownAt;
					if (elapsed < MinPlaceholderHoldMs)
					{
						await Task.Delay((int)(MinPlaceholderHoldMs - elapsed), cancellationToken).ConfigureAwait(false);
					}
				}

				// 回到 UI 线程设置图片
				await Dispatcher.UIThread.InvokeAsync(() =>
				{
					// 再次检查是否仍在视窗内且没有被新的加载任务覆盖
					if (GetLoadStatus(image) && !cancellationToken.IsCancellationRequested)
					{
						image.Source = bitmap;
						ShownUrls.AddOrUpdate(image, new ShownState { Url = url, IsReal = true });
						LogHelper.WriteLogAsync($"[图片加载] 成功设置到UI: {url}");
					}
					else
					{
						LogHelper.WriteLogAsync($"[图片加载] 加载完成但已离开视窗或取消: {url}, LoadStatus={GetLoadStatus(image)}");
					}
				});
			}
			else
			{
				LogHelper.WriteLogAsync($"[图片加载] bitmap 为 null: {url}");
			}
		}
		catch (OperationCanceledException)
		{
			LogHelper.WriteLogAsync($"[图片加载] 取消: {url}");
		}
		catch (Exception ex)
		{
			LogHelper.WriteLogAsync($"[图片加载] 异常: {url}, {ex.Message}");
		}
		finally
		{
			// 只移除自己注册的 CTS：避免在途任务结束时误删更新任务（会导致后续取消失效）
			if (PendingOperations.TryGetValue(image, out var current) && ReferenceEquals(current, cts))
			{
				PendingOperations.TryRemove(new KeyValuePair<Image, CancellationTokenSource>(image, current));
			}
		}
	}

	/// <summary>
	/// 取消并移除指定 Image 的加载任务
	/// </summary>
	private static void CancelAndRemove(Image image)
	{
		if (PendingOperations.TryRemove(image, out var oldCts))
		{
			oldCts.Cancel();
			oldCts.Dispose();
		}
	}

	/// <summary>
	/// 显示缩略图（从 Thumbnail 附加属性获取 base64 数据）
	/// </summary>
	private static void ShowThumbnail(Image sender)
	{
		var thumbnail = GetThumbnail(sender);
		ShowThumbnail(sender, thumbnail);
	}

	/// <summary>
	/// 显示缩略图（传入缩略图数据）。
	/// 同步解码（LRU 命中免解码）并设置占位，保证"先占位、后高清"的显示顺序。
	/// 关键守卫：仅当当前没有任何图片时才设置——blurhash 异步编码可能晚于真实图到达，
	/// 此时必须跳过，否则会把已显示的真实图覆盖成 blurhash 且不恢复。
	/// </summary>
	private static void ShowThumbnail(Image sender, string? thumbnail)
	{
		if (string.IsNullOrEmpty(thumbnail))
		{
			LogHelper.WriteLogAsync("[缩略图] 数据为空，跳过显示");
			return;
		}

		// 已有真实图（或已显示内容）则不覆盖
		if (sender.Source is not null)
			return;

		if (ThumbnailCache.TryGet(thumbnail, out var cached))
		{
			SetShownUrl(sender);
			sender.Source = cached;
			return;
		}

		var bitmap = DecodeThumbnail(thumbnail);
		if (bitmap == null)
			return;

		ThumbnailCache.Add(thumbnail, bitmap);
		SetShownUrl(sender);
		sender.Source = bitmap;
		LogHelper.WriteLogAsync("[缩略图] 显示成功");
	}

	/// <summary>
	/// 记录当前 Source 对应的 URL（缩略图占位，标记 IsReal=false 供两阶段过渡判断）
	/// </summary>
	private static void SetShownUrl(Image sender)
	{
		var url = GetSource(sender);
		if (!string.IsNullOrEmpty(url))
			ShownUrls.AddOrUpdate(sender, new ShownState { Url = url, IsReal = false });
	}

	private static Bitmap? DecodeThumbnail(string thumbnail)
	{
		try
		{
			// 兼容带 data URI 前缀与纯 base64 两种格式
			var base64 = thumbnail.StartsWith("data:image/webp;base64,", StringComparison.Ordinal)
				? thumbnail["data:image/webp;base64,".Length..]
				: thumbnail;
			var arr = Convert.FromBase64String(base64);
			using var ms = new MemoryStream(arr);
			return new Bitmap(ms);
		}
		catch (Exception ex)
		{
			LogHelper.WriteLogAsync($"[缩略图] 解码失败: {ex.Message}");
			return null;
		}
	}

	public static string? GetSource(Image element) => element.GetValue(SourceProperty);

	public static void SetSource(Image element, string? value) => element.SetValue(SourceProperty, value);

	public static string? GetThumbnail(Image element) => element.GetValue(ThumbnailProperty);

	public static void SetThumbnail(Image element, string? value) => element.SetValue(ThumbnailProperty, value);

	public static bool GetLoadStatus(Image element) => element.GetValue(LoadStatusProperty);

	public static void SetLoadStatus(Image element, bool value) => element.SetValue(LoadStatusProperty, value);
}
