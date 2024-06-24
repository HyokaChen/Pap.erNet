using System.Reactive.Concurrency;
using System.Threading.Tasks;
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
        Task.Run(WallpaperListViewModel.LoadWallpapersAsync);
    }

    public WallpaperListViewModel WallpaperListViewModel { get; set; } = new();
}
