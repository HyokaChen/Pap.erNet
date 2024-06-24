using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Pap.erNet.ViewModels;

namespace Pap.erNet.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        this.Focus();
    }

    private void TabChange(object? sender, SelectionChangedEventArgs e)
    {
        if (this.DataContext is MainWindowViewModel dataContext)
            Task.Run(dataContext.WallpaperListViewModel.LoadWallpapersAsync);
    }

    private void Window_Closing(object? sender, Avalonia.Controls.WindowClosingEventArgs e)
    {
        this.Hide();
        e.Cancel = true;
    }
}
