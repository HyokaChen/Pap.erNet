using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
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

		// FAB only visible on Home
		FabButton.IsVisible = _viewModel.CurrentPage == PageType.Home;

		LogHelper.WriteLogAsync($"页面切换: {_viewModel.CurrentPage}");
	}

	/// <summary>
	/// Tab 点击处理
	/// </summary>
	private void OnTabPressed(object? sender, PointerPressedEventArgs e)
	{
		if (sender is Border border && border.DataContext is HeaderWithWallpaperListViewModel clickedVm)
		{
			var items = _viewModel?.WallpaperListViewModels;
			if (items == null)
				return;

			for (var i = 0; i < items.Count; i++)
			{
				if (items[i] == clickedVm)
				{
					_viewModel!.SelectedTabIndex = i;
					UpdateTabStyles(i);
					break;
				}
			}
		}
	}

	/// <summary>
	/// 更新 Tab 选中样式
	/// </summary>
	private void UpdateTabStyles(int selectedIndex)
	{
		if (TabItemsControl?.GetVisualChildren().FirstOrDefault() is not StackPanel stackPanel)
			return;

		var children = stackPanel.GetVisualChildren().OfType<Border>().ToList();
		for (var i = 0; i < children.Count; i++)
		{
			var border = children[i];
			if (border.Child is StackPanel innerStack)
			{
				var textBlock = innerStack.Children.OfType<TextBlock>().FirstOrDefault();
				var underline = innerStack.Children.OfType<Border>().FirstOrDefault();

				if (textBlock != null)
				{
					textBlock.Foreground =
						i == selectedIndex
							? new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromArgb(255, 255, 255, 255))
							: new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromArgb(255, 160, 160, 160));
				}

				if (underline != null)
				{
					underline.Background =
						i == selectedIndex
							? new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromArgb(255, 255, 255, 255))
							: new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromArgb(0, 255, 255, 255));
				}
			}
		}
	}

	private void OnSettingsClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
	{
		_viewModel?.NavigateTo(PageType.Settings);
	}

	private void Window_Closing(object? sender, WindowClosingEventArgs e)
	{
		Hide();
		e.Cancel = true;
	}
}
