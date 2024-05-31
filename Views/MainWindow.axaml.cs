using System.Threading.Tasks;
using Avalonia.Controls;
using Pap.erNet.ViewModels;

namespace Pap.erNet.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void TabChange(object? sender, SelectionChangedEventArgs e)
    {
        if (this.DataContext is MainWindowViewModel dataContext)
            dataContext.WallpaperListViewModel.LoadWallpapers();
    }

    private void Panel_LostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e) { }
}
