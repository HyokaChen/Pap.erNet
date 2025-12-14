using System.Diagnostics;
using Avalonia.Controls;
using Pap.erNet.Utils;
using Pap.erNet.ViewModels;

namespace Pap.erNet.Pages.Home;

public partial class WallpaperListView : UserControl
{
	private bool _isUpdatingLoadStatus = false;
	private ScrollViewer? _scrollViewer = null;

	public WallpaperListView()
	{
		InitializeComponent();

		// 监听 DataContext 变化，订阅批量添加完成事件
		DataContextChanged += (s, e) =>
		{
			if (DataContext is WallpaperListViewModel vm)
			{
				vm.BatchAddingCompleted += OnBatchAddingCompleted;
			}
		};
	}

	private void OnBatchAddingCompleted()
	{
		// 批量添加完成后，手动触发一次加载状态更新
		if (_scrollViewer != null && DataContext is WallpaperListViewModel vm)
		{
			UpdateLoadStatus(_scrollViewer, vm);
		}
	}

	private void ScrollViewer_ScrollChanged(object? sender, ScrollChangedEventArgs e)
	{
		if (sender is not ScrollViewer scrollViewer)
			return;
		if (DataContext is not WallpaperListViewModel vm)
			return;

		// 保存 ScrollViewer 引用
		_scrollViewer = scrollViewer;

		// 如果列表为空，直接返回
		if (vm.WallpaperListItems.Count == 0)
			return;

		// 防止在更新加载状态时重复触发
		if (_isUpdatingLoadStatus)
			return;

		// 如果正在批量添加项目，跳过处理，等批量添加完成后再处理
		if (vm.IsBatchAdding)
		{
			LogHelper.WriteLogAsync("ScrollChanged: 正在批量添加，跳过处理");
			return;
		}

		UpdateLoadStatus(scrollViewer, vm);

		var offset = scrollViewer.Offset.Y; // 垂直偏移量
		var viewportHeight = scrollViewer.Viewport.Height; // 视口高度
		var total = scrollViewer.Extent.Height; // 可滚动内容范围
		var computeHeight = total - viewportHeight * 2; // 计算高度

		if (offset > 0 && offset >= computeHeight)
		{
			// 加载更多壁纸
			vm.LoadNextWallpapersAsync();
		}
	}

	private void UpdateLoadStatus(ScrollViewer scrollViewer, WallpaperListViewModel vm)
	{
		var offset = scrollViewer.Offset.Y; // 垂直偏移量
		var viewportHeight = scrollViewer.Viewport.Height; // 视口高度
		var itemHeight = 200; // 每个壁纸项的高度（根据WallpaperView的DesignHeight）

		// 计算可见项的范围
		var firstVisibleIndex = Math.Max(0, (int)(offset / itemHeight) - 1); // 减少1个作为缓冲
		var lastVisibleIndex = Math.Min(vm.WallpaperListItems.Count - 1, (int)((offset + viewportHeight) / itemHeight) + 1); // 增加1个作为缓冲

		LogHelper.WriteLogAsync($"可见项范围：{firstVisibleIndex} - {lastVisibleIndex}");

		// 设置标志，防止重复触发
		_isUpdatingLoadStatus = true;

		try
		{
			// 更新所有项的加载状态
			for (var i = 0; i < vm.WallpaperListItems.Count; i++)
			{
				// 只为可见项设置加载状态为true，其他项设置为false
				if (i >= firstVisibleIndex && i <= lastVisibleIndex)
				{
					vm.LoadNextStatusAsync(i);
				}
				else
				{
					vm.UnLoadNextStatusAsync(i);
				}
			}
		}
		finally
		{
			_isUpdatingLoadStatus = false;
		}
	}
}
