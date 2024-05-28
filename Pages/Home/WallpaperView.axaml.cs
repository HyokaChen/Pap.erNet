using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;

namespace Pap.erNet.Pages.Home;

public partial class WallpaperView : UserControl
{
    public WallpaperView()
    {
        InitializeComponent();
    }

    private void GotEnter(object? sender, PointerEventArgs e)
    {
        Author.IsVisible = true;
        ResolutionRatio.IsVisible = true;
    }

    private void GotExit(object? sender, PointerEventArgs e)
    {
        Author.IsVisible = false;
        ResolutionRatio.IsVisible = false;
    }
}
