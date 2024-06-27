using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Pap.erNet.Services;

namespace Pap.erNet.ViewModels;

public class WallpaperListViewModel : ViewModelBase
{
    private readonly WallpaperListService _service = new();

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
            await foreach (
                var wallpaper in _service.VerticalScreenItemsAsync().ConfigureAwait(false)
            )
            {
                var wallpaperViewModel = new WallpaperViewModel(wallpaper);
                WallpaperListItems.Add(wallpaperViewModel);
            }
        });
    }

    public ObservableCollection<WallpaperViewModel> WallpaperListItems { get; set; } = [];
}
