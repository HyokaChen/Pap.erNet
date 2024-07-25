using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using Pap.erNet.ViewModels;

namespace Pap.erNet.Views;

public partial class MainWindow : Window
{
	public MainWindow()
	{
		InitializeComponent();
		this.ShowInTaskbar = false;
		this.DataContext = App.Current?.ServicesProvider.GetRequiredService<MainWindowViewModel>();
	}

	private void Window_Closing(object? sender, Avalonia.Controls.WindowClosingEventArgs e)
	{
		this.Hide();
		e.Cancel = true;
	}

	private void TabChange(object? sender, Avalonia.Controls.SelectionChangedEventArgs e)
	{
		if (this.DataContext is MainWindowViewModel mainWindowViewModel)
		{
			if (e.Source is TabControl tabControl)
			{
				switch (tabControl.SelectedIndex)
				{
					case 0:
						mainWindowViewModel.WallpaperListViewModels[0].WallpaperListViewModel.LoadNextDiscoverWallpapersAsync();
						break;
					case 1:
						mainWindowViewModel.WallpaperListViewModels[1].WallpaperListViewModel.LoadNextLatestWallpapersAsync();
						break;
					case 2:
						mainWindowViewModel.WallpaperListViewModels[2].WallpaperListViewModel.LoadNextVerticalScreenWallpapersAsync();
						break;
					default:
						break;
				}
			}
		}
	}
}
