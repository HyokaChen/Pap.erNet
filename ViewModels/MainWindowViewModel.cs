using System.Collections.ObjectModel;

namespace Pap.erNet.ViewModels;

public class HeaderWithWallpaperListViewModel : ViewModelBase
{
	public required string Header { get; set; }
	public required WallpaperListViewModel WallpaperListViewModel { get; set; }
}

public class MainWindowViewModel : ViewModelBase
{
	public ObservableCollection<HeaderWithWallpaperListViewModel> WallpaperListViewModels { get; set; } =
	[
		new() { Header = "发现", WallpaperListViewModel = new WallpaperListViewModel() },
		new() { Header = "最新", WallpaperListViewModel = new WallpaperListViewModel() },
		new() { Header = "竖屏", WallpaperListViewModel = new WallpaperListViewModel() },
	];
}
