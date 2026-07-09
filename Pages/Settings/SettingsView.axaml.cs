using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Pap.erNet.ViewModels;

namespace Pap.erNet.Pages.Settings;

/// <summary>
/// 设置页面视图
/// 职责：处理 ViewModel 事件导航，同步 Toggle 开关的视觉状态
/// </summary>
public partial class SettingsView : UserControl
{
	private SettingsViewModel? _vm;

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
	/// toggleButton 是包含 Border 作为 Content 的 Button
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
		var (activeBtn, inactiveBtn) = isOnline ? (OnlineSourceBtn, LocalSourceBtn) : (LocalSourceBtn, OnlineSourceBtn);

		activeBtn.Background = new SolidColorBrush(Color.FromArgb(255, 74, 144, 217));
		if (activeBtn.Content is TextBlock activeText)
			activeText.Foreground = Brushes.White;

		inactiveBtn.Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
		if (inactiveBtn.Content is TextBlock inactiveText)
			inactiveText.Foreground = new SolidColorBrush(Color.FromArgb(255, 160, 160, 160));
	}

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
}
