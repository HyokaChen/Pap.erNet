using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Nito.AsyncEx;
using Pap.erNet.Models;
using Pap.erNet.Services;

namespace Pap.erNet.ViewModels;

public class WallpaperListViewModel : ViewModelBase
{
	private readonly WallpaperListService _service = new();

	private ConfiguredCancelableAsyncEnumerable<Wallpaper>.Enumerator? _wallpapersGenerator;

	private const int NEXT_BATCH = 10;

	public void LoadNextDiscoverWallpapersAsync()
	{
		Task.Run(async () =>
		{
			WallpaperListItems.Clear();
			_wallpapersGenerator = _service.DiscoverItemsAsync().ConfigureAwait(false).GetAsyncEnumerator();
			await InternalNext();
		});
	}

	public void LoadNextLatestWallpapersAsync()
	{
		Task.Run(async () =>
		{
			WallpaperListItems.Clear();
			_wallpapersGenerator = _service.LatestItemsAsync().ConfigureAwait(false).GetAsyncEnumerator();
			await InternalNext();
		});
	}

	public void LoadNextVerticalScreenWallpapersAsync()
	{
		Task.Run(async () =>
		{
			WallpaperListItems.Clear();
			_wallpapersGenerator = _service.VerticalScreenItemsAsync().ConfigureAwait(false).GetAsyncEnumerator();
			await InternalNext();
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

	public void LoadNextWallpapersAsync()
	{
		Task.Run(InternalNext);
	}

	private async Task InternalNext()
	{
		using (await _mutex.LockAsync())
		{
			Debug.WriteLine($"获取互斥锁::::");
			// TODO: need to change image source rather then append items,
			//       because it not loadd 100 items when scroll
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
	}

    private void ChangeLoadStatus()
    {
        
    }

    public ObservableCollection<WallpaperViewModel> WallpaperListItems { get; set; } = [];

	private readonly AsyncLock _mutex = new();

	~WallpaperListViewModel()
	{
		_wallpapersGenerator?.DisposeAsync();
	}
}
