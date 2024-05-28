using System;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Pap.erNet.Models;
using Pap.erNet.Utils;
using ReactiveUI;

namespace Pap.erNet.ViewModels;

public class WallpaperViewModel(Wallpaper wallpaper) : ViewModelBase
{
    private Bitmap? _imageSource;

    public Bitmap? ImageSource
    {
        get => _imageSource;
        set => this.RaiseAndSetIfChanged(ref _imageSource, value);
    }
    public string ResolutionRatio => wallpaper.ResolutionRatio;
    public string Author => wallpaper.Author;

    public async Task LoadImage()
    {
        ImageSource = await ImageHelper.LoadFromWeb((new Uri(wallpaper.Url)));
    }
}
