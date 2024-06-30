using Avalonia.Controls;
using Pap.erNet.ViewModels;

namespace Pap.erNet.Views;

public partial class MainWindow : Window
{
	public MainWindow()
	{
		InitializeComponent();
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
						mainWindowViewModel.WallpaperListViewModels[0].WallpaperListViewModel.LoadDiscoverWallpapersAsync();
						break;
					case 1:
						mainWindowViewModel.WallpaperListViewModels[1].WallpaperListViewModel.LoadLatestWallpapersAsync();
						break;
					case 2:
						mainWindowViewModel.WallpaperListViewModels[2].WallpaperListViewModel.LoadVerticalScreenWallpapersAsync();
						break;
					default:
						break;
				}
			}
		}
	}
}
