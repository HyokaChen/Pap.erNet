using Avalonia.Controls;

namespace Pap.erNet.Pages.Home;

public partial class WallpaperList : UserControl
{
    public WallpaperList()
    {
        InitializeComponent();
    }

    private void ScrollViewer_ScrollChanged(
        object? sender,
        Avalonia.Controls.ScrollChangedEventArgs e
    )
    {
        // TODO: load more in the bottom
    }
}
