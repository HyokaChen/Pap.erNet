using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using Avalonia.Threading;
using Pap.erNet.Models;
using Pap.erNet.Services;
using Pap.erNet.Utils;

namespace Pap.erNet.ViewModels;

public class WallpaperListViewModel : ViewModelBase, IDisposable
{
	private ConfiguredCancelableAsyncEnumerable<Wallpaper>.Enumerator? _wallpapersGenerator;

	// 当前生成器的取消令牌：切换分类/卸载时中断在途的分页请求
	private CancellationTokenSource? _loadCts;

	private const int NextBatch = 10;

	private int _isLoading;

	// 首屏新数据到达时需替换当前列表（缓存或旧分类内容）；加载更多批次在首屏未替换前同样作为替换
	private bool _needClear;

	// 加载代次：每次 LoadWallpapersAsync 递增；旧代次的批次到达 UI 时丢弃，防止切换竞态下的覆盖
	private int _generation;

	/// <summary>
	/// 当前分类的 ListId，用于从接口获取对应的壁纸数据
	/// </summary>
	public string ListId { get; set; } = string.Empty;

	public bool IsBatchAdding { get; private set; }

	public WallpaperListViewModel() { }

	/// <summary>
	/// 加载指定分类的壁纸（通用方法，替代原来的三个独立方法）。
	/// 切换分类时不预先清空列表，避免清空后到新数据到达之间的黑屏空窗：
	/// 1) 有缓存（上次加载的 blurhash 列表）→ 同步段立即填充显示（切换瞬间页面即有内容，无空页闪烁）；
	/// 2) 无缓存 → 旧列表保留显示；
	/// 3) 新数据首批到达时，在同一 UI 帧内 Clear+Add 原子替换。
	/// </summary>
	public void LoadWallpapersAsync()
	{
		if (Interlocked.CompareExchange(ref _isLoading, 1, 0) != 0)
		{
			LogHelper.WriteLogAsync("LoadWallpapersAsync: 已经在加载中，跳过");
			return;
		}

		// 同步段：切换 Tab 时此方法在 UI 线程被调用，立即填充缓存列表，
		// Carousel 切页瞬间新页面即有内容（blurhash 占位），不会闪现空白/黑底
		if (Dispatcher.UIThread.CheckAccess())
		{
			ShowCachedListSync();
		}
		else
		{
			// FAB 刷新等后台路径：异步补一次缓存显示
			Dispatcher.UIThread.Post(ShowCachedListSync);
		}

		Task.Run(async () =>
		{
			try
			{
				LogHelper.WriteLogAsync($"LoadWallpapersAsync: 开始加载, ListId={ListId}");
				// 先递增代次：本次加载开始后，旧代次（上一分类/上一轮）的批次在 UI 替换时将被丢弃
				Interlocked.Increment(ref _generation);
				await DisposeGeneratorAsync();

				// 首批新数据到达时必须替换当前列表（缓存或旧分类内容）
				_needClear = true;

				var cts = new CancellationTokenSource();
				_loadCts = cts;
				_wallpapersGenerator = WallpaperListService
					.GetWallpapersAsync(ListId, cts.Token)
					.ConfigureAwait(false)
					.GetAsyncEnumerator();
				await InternalNext();
			}
			finally
			{
				Interlocked.Exchange(ref _isLoading, 0);
				LogHelper.WriteLogAsync($"LoadWallpapersAsync: 加载完成, ListId={ListId}");
			}
		});
	}

	/// <summary>
	/// 立即显示缓存的列表（UI 线程）。无缓存时保留当前列表内容，等待新数据首批替换。
	/// </summary>
	private void ShowCachedListSync()
	{
		if (WallpaperListService.TryGetCachedList(ListId, out var cached) && cached.Count > 0)
		{
			LogHelper.WriteLogAsync($"LoadWallpapersAsync: 命中缓存 {cached.Count} 条，立即显示: ListId={ListId}");
			WallpaperListItems.Clear();
			foreach (var wallpaper in cached)
				WallpaperListItems.Add(new WallpaperViewModel(wallpaper));
		}
	}

	public void LoadNextWallpapersAsync()
	{
		Task.Run(async () => await InternalNext());
	}

	private async Task InternalNext()
	{
		LogHelper.WriteLogAsync($"InternalNext 开始执行，当前 Count: {WallpaperListItems.Count}");
		using (await _mutex.LockAsync())
		{
			IsBatchAdding = true;
			try
			{
				var batch = new List<WallpaperViewModel>(NextBatch);
				while (_wallpapersGenerator.HasValue && batch.Count < NextBatch)
				{
					bool hasValue;
					try
					{
						hasValue = await _wallpapersGenerator.Value.MoveNextAsync();
					}
					catch (OperationCanceledException)
					{
						// 列表已切换或卸载，静默停止
						LogHelper.WriteLogAsync("InternalNext: 已取消，停止拉取");
						break;
					}

					if (hasValue)
					{
						var wallpaper = _wallpapersGenerator.Value.Current;
						batch.Add(new WallpaperViewModel(wallpaper));
					}
					else
					{
						LogHelper.WriteLogAsync("InternalNext MoveNextAsync 返回 false，没有更多数据");
						break;
					}
				}

				if (batch.Count > 0)
				{
					// 替换与清空在同一 UI 帧内完成，消除黑屏空窗；
					// 代次校验：切换后旧代次的批次直接丢弃，避免覆盖新列表
					var gen = _generation;
					await Dispatcher.UIThread.InvokeAsync(() =>
					{
						if (gen != _generation)
							return;

						if (_needClear)
						{
							WallpaperListItems.Clear();
							_needClear = false;
						}
						foreach (var item in batch)
							WallpaperListItems.Add(item);
					});
				}
				LogHelper.WriteLogAsync($"InternalNext 完成，本次添加 {batch.Count} 个，总数: {WallpaperListItems.Count}");
			}
			finally
			{
				IsBatchAdding = false;
				// 列表变更通过 WallpaperListItems.CollectionChanged 通知视图（View 已订阅）
			}
		}
	}

	public void LoadNextStatusAsync(int startIdx)
	{
		Dispatcher.UIThread.Post(() =>
		{
			if (startIdx < WallpaperListItems.Count)
				WallpaperListItems[startIdx].IsLoad = true;
		});
	}

	public void UnLoadNextStatusAsync(int startIdx)
	{
		Dispatcher.UIThread.Post(() =>
		{
			if (startIdx < WallpaperListItems.Count)
				WallpaperListItems[startIdx].IsLoad = false;
		});
	}

	public ObservableCollection<WallpaperViewModel> WallpaperListItems { get; set; } = [];

	private readonly Nito.AsyncEx.AsyncLock _mutex = new();

	private async Task DisposeGeneratorAsync()
	{
		// 先取消在途请求，再等待生成器结束（取消后 MoveNextAsync 会快速抛出 OCE）
		_loadCts?.Cancel();
		_loadCts?.Dispose();
		_loadCts = null;

		if (_wallpapersGenerator.HasValue)
		{
			await _wallpapersGenerator.Value.DisposeAsync();
			_wallpapersGenerator = null;
		}
	}

	/// <summary>
	/// 卸载时取消在途请求并释放生成器
	/// </summary>
	public void Dispose()
	{
		_loadCts?.Cancel();
		_loadCts?.Dispose();
		_loadCts = null;

		if (_wallpapersGenerator.HasValue)
		{
			_ = _wallpapersGenerator.Value.DisposeAsync();
			_wallpapersGenerator = null;
		}

		GC.SuppressFinalize(this);
	}
}
