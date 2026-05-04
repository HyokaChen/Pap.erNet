using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using Pap.erNet.ViewModels;

namespace Pap.erNet.Views;

public partial class MainWindow : Window
{
	public MainWindow()
	{
		DataContext = App.Current?.ServicesProvider.GetRequiredService<MainWindowViewModel>();
		ShowInTaskbar = false;
		InitializeComponent();
	}

	private void Window_Closing(object? sender, WindowClosingEventArgs e)
	{
		Hide();
		e.Cancel = true;
	}

	private void TabChange(object? sender, SelectionChangedEventArgs e)
	{
		if (DataContext is MainWindowViewModel mainWindowViewModel)
		{
			if (e.Source is TabControl tabControl)
			{
				var selectedIndex = tabControl.SelectedIndex;
				if (selectedIndex >= 0 && selectedIndex < mainWindowViewModel.WallpaperListViewModels.Count)
				{
					mainWindowViewModel.WallpaperListViewModels[selectedIndex].WallpaperListViewModel.LoadWallpapersAsync();
				}
			}
		}
	}
}
