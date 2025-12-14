using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reactive.Concurrency;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Pap.erNet.Models;
using Pap.erNet.Services;
using Pap.erNet.Utils;
using ReactiveUI;

namespace Pap.erNet.ViewModels;

public class WallpaperListViewModel : ViewModelBase
{
	private readonly WallpaperListService _service = new();

	private ConfiguredCancelableAsyncEnumerable<Wallpaper>.Enumerator? _wallpapersGenerator;

	private const int NEXT_BATCH = 10;

	private int _isLoading = 0;

	private bool _isBatchAdding = false;

	public bool IsBatchAdding => _isBatchAdding;

	public event Action? BatchAddingCompleted;

	public WallpaperListViewModel()
	{
		RxApp.TaskpoolScheduler.Schedule(SubscribeLoadStatus);
	}

	public void LoadNextDiscoverWallpapersAsync()
	{
		if (Interlocked.CompareExchange(ref _isLoading, 1, 0) != 0)
		{
			LogHelper.WriteLogAsync("LoadNextDiscoverWallpapersAsync: 已经在加载中，跳过");
			return;
		}

		Task.Run(async () =>
		{
			try
			{
				LogHelper.WriteLogAsync("LoadNextDiscoverWallpapersAsync: 开始加载");
				WallpaperListItems.Clear();
				_wallpapersGenerator = _service.DiscoverItemsAsync().ConfigureAwait(false).GetAsyncEnumerator();
				await InternalNext();
			}
			finally
			{
				Interlocked.Exchange(ref _isLoading, 0);
				LogHelper.WriteLogAsync("LoadNextDiscoverWallpapersAsync: 加载完成");
			}
		});
	}

	public void LoadNextLatestWallpapersAsync()
	{
		if (Interlocked.CompareExchange(ref _isLoading, 1, 0) != 0)
		{
			LogHelper.WriteLogAsync("LoadNextLatestWallpapersAsync: 已经在加载中，跳过");
			return;
		}

		Task.Run(async () =>
		{
			try
			{
				LogHelper.WriteLogAsync("LoadNextLatestWallpapersAsync: 开始加载");
				WallpaperListItems.Clear();
				_wallpapersGenerator = _service.LatestItemsAsync().ConfigureAwait(false).GetAsyncEnumerator();
				await InternalNext();
			}
			finally
			{
				Interlocked.Exchange(ref _isLoading, 0);
				LogHelper.WriteLogAsync("LoadNextLatestWallpapersAsync: 加载完成");
			}
		});
	}

	public void LoadNextVerticalScreenWallpapersAsync()
	{
		if (Interlocked.CompareExchange(ref _isLoading, 1, 0) != 0)
		{
			LogHelper.WriteLogAsync("LoadNextVerticalScreenWallpapersAsync: 已经在加载中，跳过");
			return;
		}

		Task.Run(async () =>
		{
			try
			{
				LogHelper.WriteLogAsync("LoadNextVerticalScreenWallpapersAsync: 开始加载");
				WallpaperListItems.Clear();
				_wallpapersGenerator = _service.VerticalScreenItemsAsync().ConfigureAwait(false).GetAsyncEnumerator();
				await InternalNext();
			}
			finally
			{
				Interlocked.Exchange(ref _isLoading, 0);
				LogHelper.WriteLogAsync("LoadNextVerticalScreenWallpapersAsync: 加载完成");
			}
		});
	}

	public void LoadNextWallpapersAsync()
	{
		Task.Run(async () => await InternalNext());
	}

	private async Task InternalNext()
	{
		LogHelper.WriteLogAsync($"InternalNext 开始执行，当前 Count: {WallpaperListItems.Count}");
		var i = 0;
		using (await _mutex.LockAsync())
		{
			_isBatchAdding = true;
			try
			{
				while (_wallpapersGenerator.HasValue && i < NEXT_BATCH)
				{
					var hasValue = await _wallpapersGenerator.Value.MoveNextAsync();

					if (hasValue)
					{
						var wallpaper = _wallpapersGenerator.Value.Current;
						var wallpaperViewModel = new WallpaperViewModel(wallpaper);
						WallpaperListItems.Add(wallpaperViewModel);
						i++;
						LogHelper.WriteLogAsync($"InternalNext 添加了第 {i} 个项目，总数: {WallpaperListItems.Count}");
					}
					else
					{
						LogHelper.WriteLogAsync($"InternalNext MoveNextAsync 返回 false，没有更多数据");
						break;
					}
				}
				LogHelper.WriteLogAsync($"InternalNext 完成，本次添加 {i} 个，总数: {WallpaperListItems.Count}");
			}
			finally
			{
				_isBatchAdding = false;
				// 触发批量添加完成事件
				BatchAddingCompleted?.Invoke();
			}
		}
	}

	public void LoadNextStatusAsync(int startIdx)
	{
		Task.Run(async () =>
		{
			await LoadStatusChannel.Writer.WriteAsync((startIdx, true));
		});
	}

	public void UnLoadNextStatusAsync(int startIdx)
	{
		Task.Run(async () =>
		{
			await LoadStatusChannel.Writer.WriteAsync((startIdx, false));
		});
	}

	private async void SubscribeLoadStatus()
	{
		var reader = LoadStatusChannel.Reader;
		while (await reader.WaitToReadAsync())
		{
			if (reader.TryRead(out var itemIndex))
			{
				var (idx, status) = itemIndex;
				if (idx < WallpaperListItems.Count)
					WallpaperListItems[idx].IsLoad = status;
				else
					LogHelper.WriteLogAsync($"滚动太快了！{itemIndex}");
			}
		}
	}

	public ObservableCollection<WallpaperViewModel> WallpaperListItems { get; set; } = [];

	private readonly Nito.AsyncEx.AsyncLock _mutex = new();

	public Channel<(int, bool)> LoadStatusChannel { get; init; } =
		Channel.CreateBounded<(int, bool)>(new BoundedChannelOptions(10) { FullMode = BoundedChannelFullMode.DropOldest });

	~WallpaperListViewModel()
	{
		_wallpapersGenerator?.DisposeAsync();
	}
}
