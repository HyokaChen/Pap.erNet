using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Concurrency;
using System.Threading;
using System.Threading.Tasks;
using Pap.erNet.Services;
using ReactiveUI;

namespace Pap.erNet.ViewModels;

public class WallpaperListViewModel : ViewModelBase
{
    private readonly WallpaperListService _service = new();

    public void LoadWallpapers()
    {
        WallpaperListItems.Clear();
        var items = _service.GetItems();
        foreach (var wallpaper in items)
        {
            var wallpaperViewModel = new WallpaperViewModel(wallpaper);
            WallpaperListItems.Add(wallpaperViewModel);
        }

        // await LoadImages();
    }

    // private async Task LoadImages()
    // {
    //     foreach (var viewModel in WallpaperListItems.ToList())
    //     {
    //         await viewModel.LoadImage();
    //     }
    // }

    public ObservableCollection<WallpaperViewModel> WallpaperListItems { get; set; } = [];
}
