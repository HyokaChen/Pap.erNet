using System.Reactive.Concurrency;
using System.Windows.Input;
using ReactiveUI;

namespace Pap.erNet.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    public MainWindowViewModel()
    {
        RxApp.MainThreadScheduler.Schedule(FirstLoadImages);
    }

    private void FirstLoadImages()
    {
        WallpaperListViewModel.LoadWallpapers();
    }

    public WallpaperListViewModel WallpaperListViewModel { get; set; } = new();
}
