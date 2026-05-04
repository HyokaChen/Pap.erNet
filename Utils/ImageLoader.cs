using System.Collections.Concurrent;
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

	// 全局信号量，限制同时下载的图片数量为1，避免服务器对并发连接限流
	private static readonly SemaphoreSlim DownloadSemaphore = new(1, 1);

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
			// 离开视窗或没有 URL，显示缩略图
			if (loadStatus && string.IsNullOrEmpty(url))
			{
				ShowThumbnail(sender);
			}
			else if (!loadStatus)
			{
				ShowThumbnail(sender);
			}
			return;
		}

		// 进入视窗：先立即显示缩略图（如果已有缩略图数据）
		ShowThumbnail(sender);

		// 然后启动异步加载高清图
		var cts = new CancellationTokenSource();
		PendingOperations[sender] = cts;
		_ = LoadImageAsync(sender, url, cts.Token);
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
			ShowThumbnail(sender);

			var cts = new CancellationTokenSource();
			PendingOperations[sender] = cts;
			_ = LoadImageAsync(sender, url, cts.Token);
		}
	}

	/// <summary>
	/// 异步加载图片，加载完成后更新到 Image.Source
	/// </summary>
	private static async Task LoadImageAsync(Image image, string url, CancellationToken cancellationToken)
	{
		try
		{
			LogHelper.WriteLogAsync($"[图片加载] 开始: {url}");

			// 使用信号量限制并发下载数量
			LogHelper.WriteLogAsync($"[图片加载] 等待下载槽位: {url}");
			await DownloadSemaphore.WaitAsync(cancellationToken);
			LogHelper.WriteLogAsync($"[图片加载] 获得下载槽位: {url}");

			try
			{
				// 在后台线程加载图片
				var bitmap = await Task.Run(
						async () =>
						{
							try
							{
								return await AsyncImageLoader.ProvideImageAsync(url);
							}
							catch (Exception ex)
							{
								LogHelper.WriteLogAsync($"[图片加载] ProvideImageAsync 失败: {url}, {ex.Message}");
								return null;
							}
						},
						cancellationToken
					)
					.ConfigureAwait(false);

				if (cancellationToken.IsCancellationRequested)
				{
					LogHelper.WriteLogAsync($"[图片加载] 取消(加载完成后): {url}");
					return;
				}

				if (bitmap != null)
				{
					// 回到 UI 线程设置图片
					await Dispatcher.UIThread.InvokeAsync(() =>
					{
						// 再次检查是否仍在视窗内且没有被新的加载任务覆盖
						if (GetLoadStatus(image) && !cancellationToken.IsCancellationRequested)
						{
							image.Source = bitmap;
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
			finally
			{
				DownloadSemaphore.Release();
				LogHelper.WriteLogAsync($"[图片加载] 释放下载槽位: {url}");
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
			PendingOperations.TryRemove(image, out _);
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
	/// 显示缩略图（传入缩略图数据）
	/// </summary>
	private static void ShowThumbnail(Image sender, string? thumbnail)
	{
		if (string.IsNullOrEmpty(thumbnail))
		{
			LogHelper.WriteLogAsync("[缩略图] 数据为空，跳过显示");
			return;
		}

		try
		{
			var arr = Convert.FromBase64String(thumbnail.Replace("data:image/webp;base64,", ""));
			using var ms = new MemoryStream(arr);
			sender.Source = new Bitmap(ms);
			LogHelper.WriteLogAsync("[缩略图] 显示成功");
		}
		catch (Exception ex)
		{
			LogHelper.WriteLogAsync($"[缩略图] 显示失败: {ex.Message}");
		}
	}

	public static string? GetSource(Image element) => element.GetValue(SourceProperty);

	public static void SetSource(Image element, string? value) => element.SetValue(SourceProperty, value);

	public static string? GetThumbnail(Image element) => element.GetValue(ThumbnailProperty);

	public static void SetThumbnail(Image element, string? value) => element.SetValue(ThumbnailProperty, value);

	public static bool GetLoadStatus(Image element) => element.GetValue(LoadStatusProperty);

	public static void SetLoadStatus(Image element, bool value) => element.SetValue(LoadStatusProperty, value);
}
