using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Pap.erNet.Utils;
using Pap.erNet.ViewModels;

namespace Pap.erNet.Pages.Home;

public partial class WallpaperListView : UserControl
{
	private bool _isUpdatingLoadStatus;
	private ScrollViewer? _scrollViewer;
	private WallpaperListViewModel? _lastVm;
	private int _lastFirstVisible = -1;
	private int _lastLastVisible = -1;

	public WallpaperListView()
	{
		InitializeComponent();

		// 订阅 VM 列表集合的变更事件（Clear/Add 均在 UI 线程触发，见 WallpaperListViewModel），
		// 数据变化后重新下发可见项的加载状态。
		// 容器被虚拟化回收复用时 DataContext 会重新绑定（期间可能为 null），
		// 事件订阅的生命周期完全由 DataContextChanged 管理：退订旧 VM、订阅新 VM，
		// 并在重新绑定后立即补刷一次，兜底绑定期间错过的集合变更。
		DataContextChanged += (s, e) =>
		{
			if (_lastVm != null)
			{
				_lastVm.WallpaperListItems.CollectionChanged -= OnWallpaperListChanged;
			}

			_lastVm = DataContext as WallpaperListViewModel;
			if (_lastVm != null)
			{
				_lastVm.WallpaperListItems.CollectionChanged += OnWallpaperListChanged;
				Dispatcher.UIThread.Post(RefreshLoadStatus);
			}
		};
	}

	/// <summary>
	/// 模板应用后获取 ScrollViewer 引用。
	/// 容器被回收重建时 FindControl 可能在 realize 前落空，这里用模板命名直接获取，时序可靠。
	/// </summary>
	protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
	{
		base.OnApplyTemplate(e);
		_scrollViewer = e.NameScope.Find<ScrollViewer>("WallpaperScrollViewer");
		if (_scrollViewer != null)
		{
			Dispatcher.UIThread.Post(RefreshLoadStatus);
		}
	}

	private void OnWallpaperListChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		// 延后到当前调度队列之后执行，避免与布局/绑定相互干扰
		Dispatcher.UIThread.Post(RefreshLoadStatus);
	}

	/// <summary>
	/// 重新计算可见区间并下发加载状态
	/// </summary>
	private void RefreshLoadStatus()
	{
		var vm = _lastVm;
		if (vm == null)
			return;

		_scrollViewer ??=
			this.FindControl<ScrollViewer>("WallpaperListIC") ?? this.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
		if (_scrollViewer == null)
			return;

		// 列表重建（清空+重新加载）后旧可见区间全部失效：
		// 若新区间恰好与旧记录相同，差值逻辑会直接短路，导致新列表项永远收不到 IsLoad=true（全黑、不加载）
		_lastFirstVisible = -1;
		_lastLastVisible = -1;

		UpdateLoadStatus(_scrollViewer, vm);
	}

	private void ScrollViewer_ScrollChanged(object? sender, ScrollChangedEventArgs e)
	{
		if (sender is not ScrollViewer scrollViewer)
			return;
		if (DataContext is not WallpaperListViewModel vm)
			return;

		// 始终保存 ScrollViewer 引用
		_scrollViewer = scrollViewer;

		// 如果列表为空，直接返回
		if (vm.WallpaperListItems.Count == 0)
			return;

		// 防止在更新加载状态时重复触发
		if (_isUpdatingLoadStatus)
			return;

		// 即使在批量添加期间，也要更新可见项的加载状态
		UpdateLoadStatus(scrollViewer, vm);

		// 只有不在批量添加期间才处理加载更多
		if (vm.IsBatchAdding)
			return;

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
		const int itemHeight = 200; // 每个壁纸项的高度（根据WallpaperView的DesignHeight）
		var count = vm.WallpaperListItems.Count;

		// 计算可见项的范围
		var firstVisibleIndex = Math.Max(0, (int)(offset / itemHeight) - 1); // 减少1个作为缓冲
		var lastVisibleIndex = Math.Min(count - 1, (int)((offset + viewportHeight) / itemHeight) + 1); // 增加1个作为缓冲

		// 列表被清空重建（切换分类）时，旧可见区间失效
		if (_lastLastVisible >= count)
		{
			_lastFirstVisible = -1;
			_lastLastVisible = -1;
		}

		// 可见区间无变化：滚动仍停留在同一窗口内，无需任何下发
		if (firstVisibleIndex == _lastFirstVisible && lastVisibleIndex == _lastLastVisible)
			return;

		LogHelper.WriteLogAsync($"可见项范围：{firstVisibleIndex} - {lastVisibleIndex}");

		// 设置标志，防止重复触发
		_isUpdatingLoadStatus = true;

		try
		{
			// 只对进入/离开区间的差值项下发状态，避免每帧遍历全量列表
			var from = Math.Min(firstVisibleIndex, Math.Max(0, _lastFirstVisible));
			var to = Math.Max(lastVisibleIndex, _lastLastVisible);
			for (var i = from; i <= to; i++)
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

			_lastFirstVisible = firstVisibleIndex;
			_lastLastVisible = lastVisibleIndex;
		}
		finally
		{
			_isUpdatingLoadStatus = false;
		}
	}
}
