using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
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

		Loaded += OnWindowLoaded;
	}

	private void OnWindowLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
	{
		ApplyLogoCutout();
	}

	/// <summary>
	/// 使用 CombinedGeometry + Clip 实现 logo 镂空效果
	/// 原理：从 header 的矩形区域中 Exclude 掉文字的矢量几何体，
	/// 形成文字形状的"洞"，壁纸内容从洞中穿透显示
	/// </summary>
	private void ApplyLogoCutout()
	{
		if (HeaderMask == null)
			return;

		var headerWidth = Bounds.Width;
		var headerHeight = 56.0;

		// 1. 用 FormattedText 获取 "pap.er" 的矢量几何体
		var formattedText = new FormattedText(
			"pap.er",
			CultureInfo.CurrentCulture,
			FlowDirection.LeftToRight,
			new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Bold),
			52.0,
			Brushes.Black
		);

		// 文字位置：Padding="24,16,16,0" → 左边距24, 上边距16
		var textGeometry = formattedText.BuildGeometry(new Point(24, 8));

		// 2. 创建 header 大小的矩形
		var rectGeometry = new RectangleGeometry(new Rect(0, 0, headerWidth, headerHeight));

		// 3. 从矩形中挖掉文字形状（Exclude: A - B）
		var cutoutGeometry = new CombinedGeometry(GeometryCombineMode.Exclude, rectGeometry, textGeometry);

		// 4. 设为 HeaderMask 的 Clip —— 只有矩形减去文字的区域可见
		HeaderMask.Clip = cutoutGeometry;
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
