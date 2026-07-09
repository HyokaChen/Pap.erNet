using System.Collections.ObjectModel;
using System.Windows.Input;
using Pap.erNet.Utils;
using ReactiveUI;

namespace Pap.erNet.ViewModels;

/// <summary>
/// 应用页面枚举
/// </summary>
public enum PageType
{
	Home,
	MyWallpapers,
	Settings,
}

public class HeaderWithWallpaperListViewModel : ViewModelBase
{
	public required string Header { get; set; }

	/// <summary>
	/// 对应服务端的分类 ListId
	/// </summary>
	public required string ListId { get; set; }

	public required WallpaperListViewModel WallpaperListViewModel { get; set; }

	private bool _isSelected;

	/// <summary>
	/// 当前 Tab 是否被选中
	/// </summary>
	public bool IsSelected
	{
		get => _isSelected;
		set => this.RaiseAndSetIfChanged(ref _isSelected, value);
	}
}

public class MainWindowViewModel : ViewModelBase
{
	public ObservableCollection<HeaderWithWallpaperListViewModel> WallpaperListViewModels { get; set; } = [];

	/// <summary>
	/// 子页面 ViewModel 实例
	/// </summary>
	public SettingsViewModel SettingsViewModel { get; } = new();

	public MeViewModel Me { get; } = new();

	public MainWindowViewModel()
	{
		RefreshCurrentTabCommand = ReactiveCommand.CreateFromTask(RefreshCurrentTabAsync);

		// 监听子页面返回事件
		Me.NavigationBackRequested += () => NavigateTo(PageType.Home);
		SettingsViewModel.NavigationBackRequested += () => NavigateTo(PageType.Home);
	}

	/// <summary>
	/// 当前页面类型
	/// </summary>
	private PageType _currentPage = PageType.Home;
	public PageType CurrentPage
	{
		get => _currentPage;
		set => this.RaiseAndSetIfChanged(ref _currentPage, value);
	}

	/// <summary>
	/// 当前选中的 Tab 索引
	/// </summary>
	private int _selectedTabIndex;
	public int SelectedTabIndex
	{
		get => _selectedTabIndex;
		set
		{
			this.RaiseAndSetIfChanged(ref _selectedTabIndex, value);
			// 同步更新所有 Tab 的选中状态
			for (var i = 0; i < WallpaperListViewModels.Count; i++)
			{
				WallpaperListViewModels[i].IsSelected = i == value;
			}
			// 切换 Tab 时加载对应分类的壁纸
			if (value >= 0 && value < WallpaperListViewModels.Count)
			{
				WallpaperListViewModels[value].WallpaperListViewModel.LoadWallpapersAsync();
			}
		}
	}

	/// <summary>
	/// FAB 刷新按钮是否正在旋转
	/// </summary>
	private bool _isFabRefreshing;
	public bool IsFabRefreshing
	{
		get => _isFabRefreshing;
		set => this.RaiseAndSetIfChanged(ref _isFabRefreshing, value);
	}

	/// <summary>
	/// 刷新当前 Tab 壁纸命令（FAB 按钮）
	/// </summary>
	public ICommand RefreshCurrentTabCommand { get; }

	/// <summary>
	/// 导航到指定页面
	/// </summary>
	public void NavigateTo(PageType page)
	{
		CurrentPage = page;
	}

	/// <summary>
	/// 刷新当前选中的 Tab 壁纸列表
	/// </summary>
	private async Task RefreshCurrentTabAsync()
	{
		if (SelectedTabIndex < 0 || SelectedTabIndex >= WallpaperListViewModels.Count)
			return;

		IsFabRefreshing = true;
		LogHelper.WriteLogAsync($"FAB: 刷新 Tab {SelectedTabIndex}");

		try
		{
			await Task.Run(() => WallpaperListViewModels[SelectedTabIndex].WallpaperListViewModel.LoadWallpapersAsync());
		}
		finally
		{
			IsFabRefreshing = false;
		}
	}

	/// <summary>
	/// 从服务端获取分类列表并初始化 WallpaperListViewModels
	/// 在认证完成后调用
	/// </summary>
	public async Task InitializeListsAsync()
	{
		try
		{
			var listsResponse = await RequestUtil.GetListsAsync();
			if (listsResponse?.Data?.Lists == null || listsResponse.Data.Lists.Count == 0)
			{
				LogHelper.WriteLogAsync("MainWindowViewModel.InitializeListsAsync: 获取分类列表为空，使用默认值");
				FallbackToDefaults();
				return;
			}

			WallpaperListViewModels.Clear();
			foreach (var list in listsResponse.Data.Lists.OrderBy(l => l.Position))
			{
				WallpaperListViewModels.Add(
					new HeaderWithWallpaperListViewModel
					{
						Header = list.Name,
						ListId = list.Id,
						WallpaperListViewModel = new WallpaperListViewModel { ListId = list.Id },
					}
				);
			}

			LogHelper.WriteLogAsync($"MainWindowViewModel.InitializeListsAsync: 成功加载 {WallpaperListViewModels.Count} 个分类");

			// 自动加载第一个 Tab 的壁纸
			LoadFirstTabWallpapers();
		}
		catch (Exception ex)
		{
			LogHelper.WriteLogAsync($"MainWindowViewModel.InitializeListsAsync: 异常 - {ex.Message}");
			FallbackToDefaults();
		}
	}

	/// <summary>
	/// 降级方案：接口不可用时使用硬编码的默认分类
	/// </summary>
	/// <summary>
	/// 加载第一个 Tab 的壁纸
	/// </summary>
	private void LoadFirstTabWallpapers()
	{
		if (WallpaperListViewModels.Count > 0)
		{
			LogHelper.WriteLogAsync("MainWindowViewModel: 自动加载第一个 Tab 壁纸");
			WallpaperListViewModels[0].IsSelected = true;
			WallpaperListViewModels[0].WallpaperListViewModel.LoadWallpapersAsync();
		}
	}

	private void FallbackToDefaults()
	{
		WallpaperListViewModels.Clear();
		WallpaperListViewModels.Add(
			new HeaderWithWallpaperListViewModel
			{
				Header = "发现",
				ListId = "2244936390884196352",
				WallpaperListViewModel = new WallpaperListViewModel { ListId = "2244936390884196352" },
			}
		);
		WallpaperListViewModels.Add(
			new HeaderWithWallpaperListViewModel
			{
				Header = "最新",
				ListId = "2416408299759992832",
				WallpaperListViewModel = new WallpaperListViewModel { ListId = "2416408299759992832" },
			}
		);
		WallpaperListViewModels.Add(
			new HeaderWithWallpaperListViewModel
			{
				Header = "竖屏",
				ListId = "2245081321414066176",
				WallpaperListViewModel = new WallpaperListViewModel { ListId = "2245081321414066176" },
			}
		);

		// 降级后也自动加载第一个 Tab
		LoadFirstTabWallpapers();
	}
}
