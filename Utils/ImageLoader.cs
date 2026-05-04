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
		LoadStatusProperty.Changed.AddClassHandler<Image>(OnLoadStatusChanged);
	}

	private static readonly string TempFolder = Path.Combine(Path.GetTempPath(), "Pap.erNet");

	private static readonly DiskCachedWebImageLoader AsyncImageLoader = new(TempFolder);

	private static readonly ConcurrentDictionary<Image, CancellationTokenSource> PendingOperations = new();

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
				// 在视窗内但没有 URL，也尝试显示缩略图
				ShowThumbnail(sender);
			}
			else if (!loadStatus)
			{
				ShowThumbnail(sender);
			}
			return;
		}

		// 进入视窗：先立即显示缩略图
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
							LogHelper.WriteLogAsync($"[图片加载] 失败: {url}, {ex.Message}");
							return null;
						}
					},
					cancellationToken
				)
				.ConfigureAwait(false);

			if (cancellationToken.IsCancellationRequested)
				return;

			if (bitmap != null)
			{
				// 回到 UI 线程设置图片
				await Dispatcher.UIThread.InvokeAsync(() =>
				{
					// 再次检查是否仍在视窗内且没有被新的加载任务覆盖
					if (GetLoadStatus(image) && !cancellationToken.IsCancellationRequested)
					{
						image.Source = bitmap;
						LogHelper.WriteLogAsync($"[图片加载] 成功: {url}");
					}
				});
			}
		}
		catch (OperationCanceledException)
		{
			// 正常取消，忽略
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
	/// 在 UI 线程同步执行
	/// </summary>
	private static void ShowThumbnail(Image sender)
	{
		var thumbnail = GetThumbnail(sender);
		if (string.IsNullOrEmpty(thumbnail))
			return;

		try
		{
			var arr = Convert.FromBase64String(thumbnail.Replace("data:image/webp;base64,", ""));
			using var ms = new MemoryStream(arr);
			sender.Source = new Bitmap(ms);
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
