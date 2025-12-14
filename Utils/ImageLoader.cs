using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
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

	private static readonly string TempFolder = Path.Combine(
		Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
		"Temp"
	);

	private static DiskCachedWebImageLoader AsyncImageLoader { get; set; } = new(TempFolder);

	private static ConcurrentDictionary<Image, CancellationTokenSource> _pendingOperations = new();

	private static async void OnLoadStatusChanged(Image sender, AvaloniaPropertyChangedEventArgs args)
	{
		var loadStatus = args.GetNewValue<bool>();
		var url = GetSource(sender);

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

		if (!loadStatus || string.IsNullOrEmpty(url))
		{
			_pendingOperations.TryRemove(new KeyValuePair<Image, CancellationTokenSource>(sender, cts));
			return;
		}

		var bitmap = await Task.Run(
				async () =>
				{
					try
					{
						// A small delay allows to cancel early if the image goes out of screen too fast (eg. scrolling)
						// The Bitmap constructor is expensive and cannot be cancelled
						await Task.Delay(20, cts.Token);

						return await AsyncImageLoader.ProvideImageAsync(url!);
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

		if (bitmap != null && !cts.Token.IsCancellationRequested)
			sender.Source = bitmap;

		// "It is not guaranteed to be thread safe by ICollection, but ConcurrentDictionary's implementation is. Additionally, we recently exposed this API for .NET 5 as a public ConcurrentDictionary.TryRemove"
		_pendingOperations.TryRemove(new KeyValuePair<Image, CancellationTokenSource>(sender, cts));
	}

	private static void OnSourceChanged(Image sender, AvaloniaPropertyChangedEventArgs args)
	{
		// TODO: when in the visual windows, then request the thumb url.
		var thumbnail = GetThumbnail(sender);
		if (thumbnail != null)
		{
			var arr = Convert.FromBase64String(thumbnail.Replace("data:image/webp;base64,", ""));
			using var ms = new MemoryStream(arr);
			sender.Source = new Bitmap(ms);
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
