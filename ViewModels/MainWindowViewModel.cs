

using System.Reactive.Concurrency;
using ReactiveUI;

namespace Pap.erNet.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    public MainWindowViewModel()
    {
        RxApp.MainThreadScheduler.Schedule(FirstLoadImages);
    }

    private async void FirstLoadImages()
    {
        await WallpaperListViewModel.LoadWallpapers();
    }

    public WallpaperListViewModel WallpaperListViewModel { get; set; } = new();
}
