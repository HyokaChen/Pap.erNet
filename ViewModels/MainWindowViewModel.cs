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

    public void SetShowWallpaperWindow()
    {
        IsShowWallpaperWindow = !IsShowWallpaperWindow;
    }

    public WallpaperListViewModel WallpaperListViewModel { get; set; } = new();

    private bool _isShowWallpaperWindow;

    public bool IsShowWallpaperWindow
    {
        get => _isShowWallpaperWindow;
        set => this.RaiseAndSetIfChanged(ref _isShowWallpaperWindow, value);
    }
}
