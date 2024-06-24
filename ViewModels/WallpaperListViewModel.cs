using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Pap.erNet.Services;

namespace Pap.erNet.ViewModels;

public class WallpaperListViewModel : ViewModelBase
{
    private readonly WallpaperListService _service = new();

    public async Task LoadWallpapersAsync()
    {
        // TODO: use next to load 10 items/per
        WallpaperListItems.Clear();
        await foreach (var wallpaper in _service.DiscoverItemsAsync())
        {
            var wallpaperViewModel = new WallpaperViewModel(wallpaper);
            WallpaperListItems.Add(wallpaperViewModel);
        }
    }

    public ObservableCollection<WallpaperViewModel> WallpaperListItems { get; set; } = [];
}
