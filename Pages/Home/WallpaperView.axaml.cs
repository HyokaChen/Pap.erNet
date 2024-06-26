using System;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Pap.erNet.Utils;
using Pap.erNet.ViewModels;

namespace Pap.erNet.Pages.Home;

public partial class WallpaperView : UserControl
{
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int SystemParametersInfo(
        int uAction,
        int uParam,
        string lpvParam,
        int fuWinIni
    );

    private const int SPI_SETDESKWALLPAPER = 20;
    private const int SPIF_UPDATEINIFILE = 0x1;

    public WallpaperView()
    {
        InitializeComponent();
    }

    private void GotEnter(object? sender, PointerEventArgs e)
    {
        Author.IsVisible = true;
        ResolutionRatio.IsVisible = true;
        SetDeskWallpaper.IsVisible = true;
    }

    private void GotExit(object? sender, PointerEventArgs e)
    {
        Author.IsVisible = false;
        ResolutionRatio.IsVisible = false;
        SetDeskWallpaper.IsVisible = false;
    }

    private void SetDeskWallpaper_PointerEntered(object? sender, PointerEventArgs e)
    {
        SetDeskWallpaper.Opacity = 0.8;
    }

    private void SetDeskWallpaper_PointerExited(object? sender, PointerEventArgs e)
    {
        SetDeskWallpaper.Opacity = 0.5;
    }

    private async void SetDeskWallpaper_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // TODO: set window wallpaper with show progress bar
        DownloadPB.IsVisible = true;
        var vm = this.DataContext as WallpaperViewModel;
        var fileName = vm.ImageSource.Split("/")[^2];
        var filePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            fileName
        );
        if (!File.Exists(filePath))
        {
            var fullUrl = vm.ImageSource.Replace("/thumb", "/full");
            await DownloadAsync(fullUrl, filePath);
        }
        DownloadPB.IsVisible = false;
        _ = SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, filePath, SPIF_UPDATEINIFILE);
    }

    private async Task DownloadAsync(string fullUrl, string filePath)
    {
        using var response = await RequestUtil.HttpClient.GetAsync(
            fullUrl,
            HttpCompletionOption.ResponseHeadersRead
        );
        var contentLen = response.Content.Headers.ContentLength;
        var totalLen = contentLen ?? -1;
        await using var downloadFile = File.Create(filePath);

        await using var download = await response.Content.ReadAsStreamAsync();
        var buffer = new byte[10240];

        long totalBytesRead = 0;

        int bytesRead;

        while ((bytesRead = await download.ReadAsync(buffer).ConfigureAwait(false)) != 0)
        {
            await downloadFile.WriteAsync(buffer.AsMemory(0, bytesRead)).ConfigureAwait(false);
            totalBytesRead += bytesRead;
            Dispatcher.UIThread.Invoke(() =>
            {
                DownloadPB.Value = totalBytesRead * 1.0 / totalLen * 100;
            });
        }
    }
}
