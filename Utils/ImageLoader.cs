using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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

    public static readonly AttachedProperty<string?> SourceProperty =
        AvaloniaProperty.RegisterAttached<Image, string?>("Source", typeof(ImageLoader));

    public static readonly AttachedProperty<string?> ThumbnailProperty =
        AvaloniaProperty.RegisterAttached<Image, string?>("Thumbnail", typeof(ImageLoader));

    static ImageLoader()
    {
        SourceProperty.Changed.AddClassHandler<Image>(OnSourceChanged);
    }

    public static IAsyncImageLoader AsyncImageLoader { get; set; } = new RamCachedWebImageLoader();

    private static ConcurrentDictionary<Image, CancellationTokenSource> _pendingOperations = new();

    private static async void OnSourceChanged(Image sender, AvaloniaPropertyChangedEventArgs args)
    {
        var thumbnail = GetThumbnail(sender);
        if (thumbnail != null)
        {
            var arr = Convert.FromBase64String(thumbnail.Replace("data:image/webp;base64,", ""));
            using var ms = new MemoryStream(arr);
            sender.Source = new Bitmap(ms);
        }

        var url = args.GetNewValue<string?>();

        // Cancel/Add new pending operation
        var cts = _pendingOperations.AddOrUpdate(
            sender,
            new CancellationTokenSource(),
            (x, y) =>
            {
                y.Cancel();
                return new CancellationTokenSource();
            }
        );

        if (url == null)
        {
            _pendingOperations.TryRemove(
                new KeyValuePair<Image, CancellationTokenSource>(sender, cts)
            );
            sender.Source = null;
            return;
        }

        var bitmap = await Task.Run(
                async () =>
                {
                    try
                    {
                        // A small delay allows to cancel early if the image goes out of screen too fast (eg. scrolling)
                        // The Bitmap constructor is expensive and cannot be cancelled
                        await Task.Delay(10, cts.Token);

                        return await AsyncImageLoader.ProvideImageAsync(url);
                    }
                    catch (TaskCanceledException)
                    {
                        return null;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex.StackTrace);
                        return null;
                    }
                },
                cts.Token
            )
            .ConfigureAwait(true);

        if (bitmap != null && !cts.Token.IsCancellationRequested)
            sender.Source = bitmap!;

        // "It is not guaranteed to be thread safe by ICollection, but ConcurrentDictionary's implementation is. Additionally, we recently exposed this API for .NET 5 as a public ConcurrentDictionary.TryRemove"
        _pendingOperations.TryRemove(new KeyValuePair<Image, CancellationTokenSource>(sender, cts));
    }

    public static string? GetSource(Image element)
    {
        return element.GetValue(SourceProperty);
    }

    public static void SetSource(Image element, string? value)
    {
        element.SetValue(SourceProperty, value);
    }

    public static string? GetThumbnail(Image element)
    {
        return element.GetValue(ThumbnailProperty);
    }
}
