using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Pap.erNet.ViewModels;

namespace Pap.erNet.Pages.Settings;

/// <summary>
/// 设置页面视图
/// 职责：大标题折叠效果、Toggle 视觉状态同步、ViewModel 事件导航
/// </summary>
public partial class SettingsView : UserControl
{
	private SettingsViewModel? _vm;

	/// <summary>大标题折叠的滚动阈值（大标题区域高度约 44px）</summary>
	private const double CollapseThreshold = 44.0;

	public SettingsView()
	{
		InitializeComponent();
		DataContextChanged += OnDataContextChanged;
	}

	private void OnDataContextChanged(object? sender, EventArgs e)
	{
		if (_vm != null)
		{
			_vm.PropertyChanged -= OnViewModelPropertyChanged;
		}

		if (DataContext is SettingsViewModel vm)
		{
			_vm = vm;
			vm.PropertyChanged += OnViewModelPropertyChanged;
			vm.NavigationBackRequested -= OnNavigateBack;
			vm.NavigationBackRequested += OnNavigateBack;
			vm.QuitRequested -= OnQuitRequested;
			vm.QuitRequested += OnQuitRequested;
		}
	}

	#region 大标题折叠效果

	/// <summary>
	/// 监听滚动，实现大标题折叠：
	/// - 未滚动/在顶部：大标题 28px 完整显示，导航栏小标题透明
	/// - 滚动超过阈值：大标题缩小并淡出，导航栏小标题淡入
	/// 效果类似 iOS 设置页的 Large Title Collapse
	/// </summary>
	private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
	{
		if (LargeTitle == null || CollapsedTitle == null)
			return;

		var offset = ContentScrollViewer?.Offset.Y ?? 0;
		var progress = Math.Clamp(offset / CollapseThreshold, 0, 1);

		// 大标题：从完整大小渐隐（opacity 1→0，字体缩小 28→16）
		LargeTitle.Opacity = 1 - progress;
		LargeTitle.FontSize = 28 - (12 * progress); // 28→16
		LargeTitle.Margin = new Thickness(24, 0, 0, 16 - (16 * progress)); // 底部 margin 消失

		// 导航栏小标题：渐显
		CollapsedTitle.Opacity = progress;
	}

	#endregion

	#region Toggle 视觉状态

	private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
	{
		if (_vm == null)
			return;

		switch (e.PropertyName)
		{
			case nameof(SettingsViewModel.AutoChangeWallpaper):
				UpdateToggleVisual(AutoChangeToggle, _vm.AutoChangeWallpaper);
				break;
			case nameof(SettingsViewModel.MirrorDisplays):
				UpdateToggleVisual(MirrorDisplayToggle, _vm.MirrorDisplays);
				break;
			case nameof(SettingsViewModel.AutoImport):
				UpdateToggleVisual(AutoImportToggle, _vm.AutoImport);
				break;
			case nameof(SettingsViewModel.SameWallpaperAllSpaces):
				UpdateToggleVisual(SameWallpaperToggle, _vm.SameWallpaperAllSpaces);
				break;
			case nameof(SettingsViewModel.DimMenuBar):
				UpdateToggleVisual(DimMenuBarToggle, _vm.DimMenuBar);
				break;
			case nameof(SettingsViewModel.LaunchAtLogin):
				UpdateToggleVisual(LaunchAtLoginToggle, _vm.LaunchAtLogin);
				break;
			case nameof(SettingsViewModel.ShowInDock):
				UpdateToggleVisual(ShowInDockToggle, _vm.ShowInDock);
				break;
			case nameof(SettingsViewModel.IsOnlineSource):
				UpdateSourceSegments(_vm.IsOnlineSource);
				break;
		}
	}

	/// <summary>
	/// 更新单个 Toggle 的视觉状态（背景色 + 滑块位置）
	/// </summary>
	private static void UpdateToggleVisual(Button toggleButton, bool isOn)
	{
		if (toggleButton?.Content is not Border border)
			return;

		border.Background = new SolidColorBrush(isOn ? Color.FromArgb(255, 74, 144, 217) : Color.FromArgb(255, 50, 50, 50));

		if (border.Child is Border thumb)
		{
			thumb.HorizontalAlignment = isOn ? HorizontalAlignment.Right : HorizontalAlignment.Left;
			thumb.Margin = isOn ? new Thickness(0, 0, 2, 0) : new Thickness(2, 0, 0, 0);
		}
	}

	/// <summary>
	/// 更新来源分段控制器视觉（在线/壁纸库）
	/// </summary>
	private void UpdateSourceSegments(bool isOnline)
	{
		var (activeBorder, inactiveBorder) = isOnline ? (OnlineSourceBtn, LocalSourceBtn) : (LocalSourceBtn, OnlineSourceBtn);

		activeBorder.Background = new SolidColorBrush(Color.FromArgb(255, 74, 144, 217));
		if (activeBorder.Child is TextBlock activeText)
			activeText.Foreground = Brushes.White;

		inactiveBorder.Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
		if (inactiveBorder.Child is TextBlock inactiveText)
			inactiveText.Foreground = new SolidColorBrush(Color.FromArgb(255, 160, 160, 160));
	}

	/// <summary>点击“在线”</summary>
	private void OnOnlineSourceClicked(object? sender, PointerPressedEventArgs e)
	{
		_vm?.SwitchToOnlineSourceCommand.Execute(null);
	}

	/// <summary>点击“壁纸库”</summary>
	private void OnLocalSourceClicked(object? sender, PointerPressedEventArgs e)
	{
		_vm?.SwitchToLocalSourceCommand.Execute(null);
	}

	#endregion

	#region 导航和退出

	private void OnNavigateBack()
	{
		if (VisualRoot is Views.MainWindow mainWindow && mainWindow.DataContext is MainWindowViewModel mvm)
		{
			mvm.NavigateTo(PageType.Home);
		}
	}

	private static void OnQuitRequested()
	{
		if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			desktop.Shutdown();
		}
	}

	#endregion
}
