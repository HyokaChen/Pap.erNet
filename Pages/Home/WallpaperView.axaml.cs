using Avalonia.Controls;
using Avalonia.Input;
using System.Runtime.InteropServices;

namespace Pap.erNet.Pages.Home;

public partial class WallpaperView : UserControl
{
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);
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

    private void SetDeskWallpaper_PointerEntered(object? sender, Avalonia.Input.PointerEventArgs e)
    {
        SetDeskWallpaper.Opacity = 0.8;
    }

    private void SetDeskWallpaper_PointerExited(object? sender, Avalonia.Input.PointerEventArgs e)
    {
        SetDeskWallpaper.Opacity = 0.5;
    }

    private void SetDeskWallpaper_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        // TODO: set window wallpaper
        _ = SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, "", SPIF_UPDATEINIFILE);
    }
}
