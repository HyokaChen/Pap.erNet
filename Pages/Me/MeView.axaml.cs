using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Pap.erNet.ViewModels;

namespace Pap.erNet.Pages.Me;

/// <summary>
/// 我的壁纸页面视图
/// 职责：处理 hover 交互、ViewModel 事件导航，同步随机壁纸开关视觉状态
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

		if (e.PropertyName == nameof(MeViewModel.RandomWallpaperEnabled))
		{
			UpdateRandomBadge(_vm.RandomWallpaperEnabled);
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

	#region 壁纸卡片 Hover 交互

	/// <summary>
	/// 鼠标进入壁纸卡片：显示分辨率标签、设为桌面按钮、删除按钮
	/// </summary>
	private void OnCardPointerEntered(object? sender, PointerEventArgs e)
	{
		if (sender is Border cardBorder)
		{
			SetChildVisibility(cardBorder, "ResolutionBadge", true);
			SetChildVisibility(cardBorder, "SetDesktopBtn", true);
			SetChildVisibility(cardBorder, "DeleteBtn", true);
		}
	}

	/// <summary>
	/// 鼠标离开壁纸卡片：隐藏所有 hover 控件
	/// </summary>
	private void OnCardPointerExited(object? sender, PointerEventArgs e)
	{
		if (sender is Border cardBorder)
		{
			SetChildVisibility(cardBorder, "ResolutionBadge", false);
			SetChildVisibility(cardBorder, "SetDesktopBtn", false);
			SetChildVisibility(cardBorder, "DeleteBtn", false);
		}
	}

	/// <summary>
	/// 按 Name 查找子元素并设置可见性
	/// </summary>
	private static void SetChildVisibility(Control parent, string name, bool visible)
	{
		var child = parent.FindControl<Control>(name);
		if (child != null)
		{
			child.IsVisible = visible;
		}
	}

	/// <summary>
	/// 设为桌面按钮点击
	/// </summary>
	private void OnSetDesktopPressed(object? sender, PointerPressedEventArgs e)
	{
		// 向上找到卡片 Border，获取 DataContext
		if (sender is Control btn)
		{
			var parent = btn.Parent;
			while (parent != null)
			{
				if (parent is Border border && border.DataContext is WallpaperCardViewModel card)
				{
					card.SetDesktopCommand.Execute(null);
					return;
				}
				parent = parent.Parent;
			}
		}
	}

	#endregion
}
