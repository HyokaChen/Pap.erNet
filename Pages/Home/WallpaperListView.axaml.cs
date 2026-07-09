using Avalonia.Controls;
using Avalonia.VisualTree;
using Pap.erNet.Utils;
using Pap.erNet.ViewModels;

namespace Pap.erNet.Pages.Home;

public partial class WallpaperListView : UserControl
{
	private bool _isUpdatingLoadStatus;

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
		if (DataContext is WallpaperListViewModel vm)
		{
			LogHelper.WriteLogAsync("OnBatchAddingCompleted: 批量添加完成");

			Avalonia.Threading.Dispatcher.UIThread.Post(() =>
			{
				UpdateLoadStatus(WallpaperScrollViewer, vm);
			});
		}
	}

	private void ScrollViewer_ScrollChanged(object? sender, ScrollChangedEventArgs e)
	{
		if (sender is not ScrollViewer scrollViewer)
			return;
		if (DataContext is not WallpaperListViewModel vm)
			return;

		if (vm.WallpaperListItems.Count == 0)
			return;

		if (_isUpdatingLoadStatus)
			return;

		UpdateLoadStatus(scrollViewer, vm);

		if (vm.IsBatchAdding)
			return;

		var offset = scrollViewer.Offset.Y;
		var viewportHeight = scrollViewer.Viewport.Height;
		var total = scrollViewer.Extent.Height;
		var computeHeight = total - viewportHeight * 2;

		if (offset > 0 && offset >= computeHeight)
		{
			vm.LoadNextWallpapersAsync();
		}
	}

	private void UpdateLoadStatus(ScrollViewer scrollViewer, WallpaperListViewModel vm)
	{
		var offset = scrollViewer.Offset.Y;
		var viewportHeight = scrollViewer.Viewport.Height;
		const int itemHeight = 256; // 每个壁纸项的高度约 212px (200 image + 12 margin) + buffer

		var firstVisibleIndex = Math.Max(0, (int)(offset / itemHeight) - 1);
		var lastVisibleIndex = Math.Min(vm.WallpaperListItems.Count - 1, (int)((offset + viewportHeight) / itemHeight) + 1);

		_isUpdatingLoadStatus = true;

		try
		{
			for (var i = 0; i < vm.WallpaperListItems.Count; i++)
			{
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
