using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Media.Imaging;
using Pap.erNet.Models;
using Pap.erNet.Utils;
using ReactiveUI;

namespace Pap.erNet.ViewModels;

public class WallpaperViewModel(Wallpaper wallpaper) : ViewModelBase
{
    // private Bitmap? _imageSource;
    //
    // public Bitmap? ImageSource
    // {
    //     get => _imageSource;
    //     set => this.RaiseAndSetIfChanged(ref _imageSource, value);
    // }

    public ICommand VisitBrowserCommand { get; } =
        ReactiveCommand.Create(
            (string url) =>
            {
                if (RuntimeInformation.IsOSPlatform((OSPlatform.Linux)))
                {
                    ShellExec($"xdg-open {url}", waitForExit: false);
                }
                else
                {
                    using var process = Process.Start(
                        new ProcessStartInfo()
                        {
                            FileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                                ? url
                                : "open",
                            Arguments = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                                ? $"{url}"
                                : "",
                            CreateNoWindow = true,
                            UseShellExecute = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                        }
                    );
                }
            }
        );

    public string ThumbnailSource => wallpaper.Thumbnail;
    public string ImageSource => wallpaper.Url;
    public string ResolutionRatio => wallpaper.ResolutionRatio;
    public string Author => wallpaper.Author;

    // public async Task LoadImage()
    // {
    //      ImageSource = await ImageHelper.LoadFromWeb((new Uri(wallpaper.Url)));
    // }

    private static void ShellExec(string cmd, bool waitForExit = true)
    {
        var escapeArgs = cmd.Replace("\"", "\\\"");
        using var process = Process.Start(
            new ProcessStartInfo()
            {
                FileName = "/bin/sh",
                Arguments = $"-c \"{escapeArgs}\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            }
        );
        if (waitForExit)
            process!.WaitForExit();
    }
}
