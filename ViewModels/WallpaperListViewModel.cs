using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Concurrency;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using System.Threading.Tasks;
using Pap.erNet.Models;
using Pap.erNet.Services;
using ReactiveUI;

namespace Pap.erNet.ViewModels;

public class WallpaperListViewModel : ViewModelBase
{
	private readonly WallpaperListService _service = new();

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

			foreach (var idx in Enumerable.Range(0, 10))
			{
				tasks.Add(
					Task.Run(async () =>
					{
						await LoadStatusChannel.Writer.WriteAsync(idx);
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
			Debug.WriteLine($"滚动塞了多少数据:({startIdx + 1}->{startIdx + 4})");
			List<Task> tasks = new(4);

			foreach (var idx in Enumerable.Range(startIdx + 1, 4))
			{
				tasks.Add(
					Task.Run(async () =>
					{
						await LoadStatusChannel.Writer.WriteAsync(idx);
					})
				);
			}
			await Task.WhenAll([.. tasks]);
		});
	}

	private async void SubscribeLoadStatus()
	{
		var reader = LoadStatusChannel.Reader;
		while (await reader.WaitToReadAsync())
		{
			if (reader.TryRead(out var itemIndex))
			{
				if (itemIndex < WallpaperListItems.Count)
					WallpaperListItems[itemIndex].IsLoad = true;
				else
					Debug.WriteLine($"滚动太快了！{itemIndex}");
			}
		}
	}

	public ObservableCollection<WallpaperViewModel> WallpaperListItems { get; set; } = [];

	private readonly Nito.AsyncEx.AsyncLock _mutex = new();

	public Channel<int> LoadStatusChannel { get; init; } =
		Channel.CreateBounded<int>(new BoundedChannelOptions(10) { FullMode = BoundedChannelFullMode.DropOldest });

	~WallpaperListViewModel()
	{
		_wallpapersGenerator?.DisposeAsync();
	}
}
