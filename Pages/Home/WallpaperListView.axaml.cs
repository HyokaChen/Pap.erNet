using System.Diagnostics;
using Avalonia.Controls;
using Pap.erNet.Utils;
using Pap.erNet.ViewModels;

namespace Pap.erNet.Pages.Home;

public partial class WallpaperListView : UserControl
{
	public WallpaperListView()
	{
		InitializeComponent();
	}

	private async void ScrollViewer_ScrollChanged(object? sender, ScrollChangedEventArgs e)
	{
		if (sender is not ScrollViewer scrollViewer)
			return;
		if (DataContext is not WallpaperListViewModel vm)
			return;

		var offset = scrollViewer.Offset.Y; // 垂直偏移量
		var viewportHeight = scrollViewer.Viewport.Height; // 视口高度
		var itemHeight = 200; // 每个壁纸项的高度（根据WallpaperView的DesignHeight）

		// 计算可见项的范围
		var firstVisibleIndex = Math.Max(0, (int)(offset / itemHeight) - 1); // 减少1个作为缓冲
		var lastVisibleIndex = Math.Min(vm.WallpaperListItems.Count - 1, (int)((offset + viewportHeight) / itemHeight) + 1); // 增加1个作为缓冲

		// 重置所有项的加载状态为false
		for (var i = 0; i < vm.WallpaperListItems.Count; i++)
		{
			vm.WallpaperListItems[i].IsLoad = false;
		}

		// 只为可见项设置加载状态为true
		for (int i = firstVisibleIndex; i <= lastVisibleIndex; i++)
		{
			if (i >= 0 && i < vm.WallpaperListItems.Count)
			{
				vm.WallpaperListItems[i].IsLoad = true;
			}
		}

		var total = scrollViewer.Extent.Height; // 可滚动内容范围
		var computeHeight = total - viewportHeight * 2; // 计算高度

		if (offset > 0 && offset >= computeHeight)
		{
			// 加载更多壁纸
			vm.LoadNextWallpapersAsync();
		}

		if (offset <= 0)
		{
			LogHelper.WriteLogAsync($"初次请求了吗？{vm.WallpaperListItems.Count}");
		}
	}
}
