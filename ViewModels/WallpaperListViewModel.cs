using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reactive.Concurrency;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Pap.erNet.Models;
using Pap.erNet.Services;
using ReactiveUI;

namespace Pap.erNet.ViewModels;

public class WallpaperListViewModel : ViewModelBase
{
	private readonly WallpaperListService _service = new();

	private readonly IReadOnlyList<int> RUN_RANGE_LIST = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9];

	private ConfiguredCancelableAsyncEnumerable<Wallpaper>.Enumerator? _wallpapersGenerator;

	private const int NEXT_BATCH = 10;

	public WallpaperListViewModel()
	{
		RxApp.TaskpoolScheduler.Schedule(SubscribeLoadStatus);
	}

	public void LoadNextDiscoverWallpapersAsync()
	{
		Task.Run(async () =>
		{
			WallpaperListItems.Clear();
			_wallpapersGenerator = _service.DiscoverItemsAsync().ConfigureAwait(false).GetAsyncEnumerator();
			await InternalNext(true);
		});
	}

	public void LoadNextLatestWallpapersAsync()
	{
		Task.Run(async () =>
		{
			WallpaperListItems.Clear();
			_wallpapersGenerator = _service.LatestItemsAsync().ConfigureAwait(false).GetAsyncEnumerator();
			await InternalNext(true);
		});
	}

	public void LoadNextVerticalScreenWallpapersAsync()
	{
		Task.Run(async () =>
		{
			WallpaperListItems.Clear();
			_wallpapersGenerator = _service.VerticalScreenItemsAsync().ConfigureAwait(false).GetAsyncEnumerator();
			await InternalNext(true);
		});
	}

	public void LoadDiscoverWallpapersAsync()
	{
		Task.Run(async () =>
		{
			WallpaperListItems.Clear();
			await foreach (var wallpaper in _service.DiscoverItemsAsync().ConfigureAwait(false))
			{
				var wallpaperViewModel = new WallpaperViewModel(wallpaper);
				WallpaperListItems.Add(wallpaperViewModel);
			}
		});
	}

	public void LoadLatestWallpapersAsync()
	{
		Task.Run(async () =>
		{
			WallpaperListItems.Clear();
			await foreach (var wallpaper in _service.LatestItemsAsync().ConfigureAwait(false))
			{
				var wallpaperViewModel = new WallpaperViewModel(wallpaper);
				WallpaperListItems.Add(wallpaperViewModel);
			}
		});
	}

	public void LoadVerticalScreenWallpapersAsync()
	{
		Task.Run(async () =>
		{
			WallpaperListItems.Clear();
			await foreach (var wallpaper in _service.VerticalScreenItemsAsync().ConfigureAwait(false))
			{
				var wallpaperViewModel = new WallpaperViewModel(wallpaper);
				WallpaperListItems.Add(wallpaperViewModel);
			}
		});
	}

	public void LoadNextWallpapersAsync(bool needInitLoadStatus = false)
	{
		Task.Run(async () => await InternalNext(needInitLoadStatus));
	}

	private async Task InternalNext(bool needInitLoadStatus)
	{
		using (await _mutex.LockAsync())
		{
			int i = 0;
			while (_wallpapersGenerator.HasValue && i < NEXT_BATCH)
			{
				var hasValue = await _wallpapersGenerator.Value.MoveNextAsync();

				if (hasValue)
				{
					var wallpaper = _wallpapersGenerator.Value.Current;
					var wallpaperViewModel = new WallpaperViewModel(wallpaper);
					WallpaperListItems.Add(wallpaperViewModel);
					i++;
				}
			}
			Debug.WriteLine($"WallpaperListItems's count:::{WallpaperListItems.Count}");
		}
		if (needInitLoadStatus)
		{
			List<Task> tasks = new(10);

			foreach (var idx in RUN_RANGE_LIST)
			{
				tasks.Add(
					Task.Run(async () =>
					{
						await LoadStatusChannel.Writer.WriteAsync((idx, true));
					})
				);
			}
			await Task.WhenAll([.. tasks]);
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
					Debug.WriteLine($"滚动太快了！{itemIndex}");
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
