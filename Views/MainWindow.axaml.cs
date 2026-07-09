using Avalonia.Controls;
using Avalonia.Input;
using Microsoft.Extensions.DependencyInjection;
using Pap.erNet.Utils;
using Pap.erNet.ViewModels;

namespace Pap.erNet.Views;

public partial class MainWindow : Window
{
	private MainWindowViewModel? _viewModel;

	public MainWindow()
	{
		_viewModel = App.Current?.ServicesProvider.GetRequiredService<MainWindowViewModel>();
		DataContext = _viewModel;
		ShowInTaskbar = false;
		InitializeComponent();

		if (_viewModel != null)
		{
			_viewModel.PropertyChanged += OnViewModelPropertyChanged;
		}
	}

	private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(MainWindowViewModel.CurrentPage))
		{
			UpdatePageVisibility();
		}
	}

	private void UpdatePageVisibility()
	{
		if (_viewModel == null)
			return;

		HomePage.IsVisible = _viewModel.CurrentPage == PageType.Home;
		SettingsPage.IsVisible = _viewModel.CurrentPage == PageType.Settings;
		MyWallpapersPage.IsVisible = _viewModel.CurrentPage == PageType.MyWallpapers;
		FabButton.IsVisible = _viewModel.CurrentPage == PageType.Home;

		LogHelper.WriteLogAsync($"页面切换: {_viewModel.CurrentPage}");
	}

	private void OnTabPressed(object? sender, PointerPressedEventArgs e)
	{
		if (sender is Border { DataContext: HeaderWithWallpaperListViewModel clickedVm } && _viewModel != null)
		{
			var items = _viewModel.WallpaperListViewModels;
			for (var i = 0; i < items.Count; i++)
			{
				if (items[i] == clickedVm)
				{
					_viewModel.SelectedTabIndex = i;
					break;
				}
			}
		}
	}

	private void OnSettingsClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
	{
		_viewModel?.NavigateTo(PageType.Settings);
	}

	private void OnMyWallpapersClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
	{
		_viewModel?.NavigateTo(PageType.MyWallpapers);
	}

	private void Window_Closing(object? sender, WindowClosingEventArgs e)
	{
		Hide();
		e.Cancel = true;
	}
}
