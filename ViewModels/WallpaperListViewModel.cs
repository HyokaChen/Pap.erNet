using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using Pap.erNet.Models;
using Pap.erNet.Services;
using Pap.erNet.Utils;

namespace Pap.erNet.ViewModels;

public class WallpaperListViewModel : ViewModelBase
{
	private readonly WallpaperListService _service = new();

	private ConfiguredCancelableAsyncEnumerable<Wallpaper>.Enumerator? _wallpapersGenerator;

	private const int NextBatch = 10;

	private int _isLoading;

	/// <summary>
	/// 当前分类的 ListId，用于从接口获取对应的壁纸数据
	/// </summary>
	public string ListId { get; set; } = string.Empty;

	public bool IsBatchAdding { get; private set; }

	public event Action? BatchAddingCompleted;

	public WallpaperListViewModel() { }

	/// <summary>
	/// 加载指定分类的壁纸（通用方法，替代原来的三个独立方法）
	/// </summary>
	public void LoadWallpapersAsync()
	{
		if (Interlocked.CompareExchange(ref _isLoading, 1, 0) != 0)
		{
			LogHelper.WriteLogAsync("LoadWallpapersAsync: 已经在加载中，跳过");
			return;
		}

		Task.Run(async () =>
		{
			try
			{
				LogHelper.WriteLogAsync($"LoadWallpapersAsync: 开始加载, ListId={ListId}");
				WallpaperListItems.Clear();
				await DisposeGeneratorAsync();
				_wallpapersGenerator = _service.GetWallpapersAsync(ListId).ConfigureAwait(false).GetAsyncEnumerator();
				await InternalNext();
			}
			finally
			{
				Interlocked.Exchange(ref _isLoading, 0);
				LogHelper.WriteLogAsync($"LoadWallpapersAsync: 加载完成, ListId={ListId}");
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
			IsBatchAdding = true;
			try
			{
				while (_wallpapersGenerator.HasValue && i < NextBatch)
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
						LogHelper.WriteLogAsync("InternalNext MoveNextAsync 返回 false，没有更多数据");
						break;
					}
				}
				LogHelper.WriteLogAsync($"InternalNext 完成，本次添加 {i} 个，总数: {WallpaperListItems.Count}");
			}
			finally
			{
				IsBatchAdding = false;
				// 触发批量添加完成事件
				BatchAddingCompleted?.Invoke();
			}
		}
	}

	public void LoadNextStatusAsync(int startIdx)
	{
		Avalonia.Threading.Dispatcher.UIThread.Post(() =>
		{
			if (startIdx < WallpaperListItems.Count)
				WallpaperListItems[startIdx].IsLoad = true;
		});
	}

	public void UnLoadNextStatusAsync(int startIdx)
	{
		Avalonia.Threading.Dispatcher.UIThread.Post(() =>
		{
			if (startIdx < WallpaperListItems.Count)
				WallpaperListItems[startIdx].IsLoad = false;
		});
	}

	public ObservableCollection<WallpaperViewModel> WallpaperListItems { get; set; } = [];

	private readonly Nito.AsyncEx.AsyncLock _mutex = new();

	private async Task DisposeGeneratorAsync()
	{
		if (_wallpapersGenerator.HasValue)
		{
			await _wallpapersGenerator.Value.DisposeAsync();
			_wallpapersGenerator = null;
		}
	}

	~WallpaperListViewModel()
	{
		_wallpapersGenerator?.DisposeAsync();
	}
}
