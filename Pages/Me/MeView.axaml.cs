using Avalonia.Controls;
using Avalonia.Media;
using Pap.erNet.ViewModels;

namespace Pap.erNet.Pages.Me;

/// <summary>
/// 我的壁纸页面视图
/// 职责：处理 ViewModel 事件导航，同步随机壁纸开关视觉状态
/// </summary>
public partial class MeView : UserControl
{
	private MeViewModel? _vm;

	public MeView()
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

		if (DataContext is MeViewModel vm)
		{
			_vm = vm;
			vm.PropertyChanged += OnViewModelPropertyChanged;
			vm.NavigationBackRequested -= OnNavigateBack;
			vm.NavigationBackRequested += OnNavigateBack;
		}
	}

	private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
	{
		if (_vm == null)
			return;

		switch (e.PropertyName)
		{
			case nameof(MeViewModel.RandomWallpaperEnabled):
				UpdateRandomBadge(_vm.RandomWallpaperEnabled);
				break;
		}
	}

	/// <summary>
	/// 更新随机壁纸状态徽章
	/// </summary>
	private void UpdateRandomBadge(bool isOn)
	{
		RandomStatusBadge.Background = new SolidColorBrush(isOn ? Color.FromArgb(255, 74, 144, 217) : Color.FromArgb(255, 50, 50, 50));
		RandomStatusText.Foreground = new SolidColorBrush(isOn ? Colors.White : Color.FromArgb(255, 110, 110, 110));
	}

	private void OnNavigateBack()
	{
		if (VisualRoot is Views.MainWindow mainWindow && mainWindow.DataContext is MainWindowViewModel mvm)
		{
			mvm.NavigateTo(PageType.Home);
		}
	}
}
