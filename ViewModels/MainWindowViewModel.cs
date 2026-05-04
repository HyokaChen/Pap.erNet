using System.Collections.ObjectModel;
using Pap.erNet.Models;
using Pap.erNet.Utils;

namespace Pap.erNet.ViewModels;

public class HeaderWithWallpaperListViewModel : ViewModelBase
{
	public required string Header { get; set; }

	/// <summary>
	/// 对应服务端的分类 ListId
	/// </summary>
	public required string ListId { get; set; }

	public required WallpaperListViewModel WallpaperListViewModel { get; set; }
}

public class MainWindowViewModel : ViewModelBase
{
	public ObservableCollection<HeaderWithWallpaperListViewModel> WallpaperListViewModels { get; set; } = [];

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
	}
}
