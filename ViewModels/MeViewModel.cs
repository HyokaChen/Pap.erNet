// Copyright (c) 2024 Pap.erNet. All rights reserved.
// 功能：我的壁纸页面视图模型，管理已收藏壁纸列表、随机壁纸控制及单张壁纸操作
// 作者：Pap.erNet 团队

using System.Collections.ObjectModel;
using System.Windows.Input;
using Pap.erNet.Models;
using Pap.erNet.Utils;
using ReactiveUI;

namespace Pap.erNet.ViewModels;

/// <summary>
/// 单张壁纸卡片视图模型（用于我的壁纸列表）
/// </summary>
public class WallpaperCardViewModel : ViewModelBase
{
	private readonly Wallpaper _wallpaper;

	public WallpaperCardViewModel(Wallpaper wallpaper)
	{
		_wallpaper = wallpaper;
		DeleteCommand = ReactiveCommand.Create(Delete);
		SetDesktopCommand = ReactiveCommand.Create(SetDesktop);
	}

	/// <summary>壁纸 ID</summary>
	public string Id => _wallpaper.Id;

	/// <summary>高清图 URL</summary>
	public string ImageUrl => _wallpaper.Url;

	/// <summary>缩略图 base64</summary>
	public string Thumbnail => _wallpaper.Thumbnail;

	/// <summary>分辨率标签</summary>
	public string ResolutionRatio => _wallpaper.ResolutionRatio;

	/// <summary>作者</summary>
	public string Author => _wallpaper.Author;

	/// <summary>链接</summary>
	public string Link => _wallpaper.Link;

	/// <summary>是否为 4K 分辨率</summary>
	public bool Is4K => ResolutionRatio.Contains("3840") || ResolutionRatio.Contains("4096") || ResolutionRatio.Contains("2160");

	/// <summary>是否正在加载图片</summary>
	private bool _isLoad;
	public bool IsLoad
	{
		get => _isLoad;
		set => this.RaiseAndSetIfChanged(ref _isLoad, value);
	}

	/// <summary>删除命令</summary>
	public ICommand DeleteCommand { get; }

	/// <summary>设为桌面命令</summary>
	public ICommand SetDesktopCommand { get; }

	private void Delete()
	{
		LogHelper.WriteLogAsync($"删除壁纸: {Id}");
		// TODO: 调用服务端/本地删除逻辑
	}

	private void SetDesktop()
	{
		LogHelper.WriteLogAsync($"设为桌面: {Id}");
		// TODO: 调用系统壁纸设置逻辑
	}
}

/// <summary>
/// 我的壁纸页面视图模型
/// 职责：管理已收藏/下载的壁纸列表、随机壁纸开关及双屏预览状态
/// </summary>
public class MeViewModel : ViewModelBase
{
	#region 随机壁纸

	private bool _randomWallpaperEnabled;

	/// <summary>随机壁纸开关</summary>
	public bool RandomWallpaperEnabled
	{
		get => _randomWallpaperEnabled;
		set
		{
			this.RaiseAndSetIfChanged(ref _randomWallpaperEnabled, value);
			this.RaisePropertyChanged(nameof(RandomStatusText));
			this.RaisePropertyChanged(nameof(RandomStatusBrushIndex));
		}
	}

	/// <summary>随机壁纸状态文字</summary>
	public string RandomStatusText => RandomWallpaperEnabled ? "ON" : "OFF";

	/// <summary>0=OFF, 1=ON（用于 UI 转换）</summary>
	public int RandomStatusBrushIndex => RandomWallpaperEnabled ? 1 : 0;

	/// <summary>主显示器预览壁纸</summary>
	private Wallpaper? _primaryWallpaper;
	public Wallpaper? PrimaryWallpaper
	{
		get => _primaryWallpaper;
		set => this.RaiseAndSetIfChanged(ref _primaryWallpaper, value);
	}

	/// <summary>副显示器预览壁纸</summary>
	private Wallpaper? _secondaryWallpaper;
	public Wallpaper? SecondaryWallpaper
	{
		get => _secondaryWallpaper;
		set => this.RaiseAndSetIfChanged(ref _secondaryWallpaper, value);
	}

	#endregion

	#region 壁纸列表

	/// <summary>壁纸卡片 ViewModel 集合</summary>
	public ObservableCollection<WallpaperCardViewModel> WallpaperCards { get; } = [];

	private int _totalCount;

	/// <summary>壁纸总数</summary>
	public int TotalCount
	{
		get => _totalCount;
		set
		{
			this.RaiseAndSetIfChanged(ref _totalCount, value);
			this.RaisePropertyChanged(nameof(PageTitleText));
			this.RaisePropertyChanged(nameof(IsEmpty));
			this.RaisePropertyChanged(nameof(HasWallpapers));
		}
	}

	/// <summary>页面标题</summary>
	public string PageTitleText => $"我的壁纸 ({TotalCount})";

	/// <summary>是否为空状态</summary>
	public bool IsEmpty => TotalCount == 0;

	/// <summary>是否有壁纸</summary>
	public bool HasWallpapers => TotalCount > 0;

	#endregion

	#region 命令

	/// <summary>返回首页</summary>
	public ICommand NavigateBackCommand { get; }

	/// <summary>切换随机壁纸开关</summary>
	public ICommand ToggleRandomWallpaperCommand { get; }

	/// <summary>导入壁纸（打开文件夹）</summary>
	public ICommand ImportWallpapersCommand { get; }

	/// <summary>下载主屏壁纸</summary>
	public ICommand DownloadPrimaryCommand { get; }

	/// <summary>下载副屏壁纸</summary>
	public ICommand DownloadSecondaryCommand { get; }

	#endregion

	public MeViewModel()
	{
		NavigateBackCommand = ReactiveCommand.Create(NavigateBack);
		ToggleRandomWallpaperCommand = ReactiveCommand.Create(ToggleRandomWallpaper);
		ImportWallpapersCommand = ReactiveCommand.Create(ImportWallpapers);
		DownloadPrimaryCommand = ReactiveCommand.Create(DownloadPrimary);
		DownloadSecondaryCommand = ReactiveCommand.Create(DownloadSecondary);
	}

	/// <summary>请求导航回首页事件</summary>
	public event Action? NavigationBackRequested;

	#region 方法

	private void NavigateBack()
	{
		LogHelper.WriteLogAsync("MeViewModel: 返回首页");
		NavigationBackRequested?.Invoke();
	}

	private void ToggleRandomWallpaper()
	{
		RandomWallpaperEnabled = !RandomWallpaperEnabled;
		LogHelper.WriteLogAsync($"随机壁纸: {(RandomWallpaperEnabled ? "开启" : "关闭")}");
	}

	private void ImportWallpapers()
	{
		LogHelper.WriteLogAsync("MeViewModel: 导入壁纸");
		// TODO: 打开文件/文件夹选择器
	}

	private void DownloadPrimary()
	{
		LogHelper.WriteLogAsync("MeViewModel: 下载主屏壁纸");
		// TODO: 下载当前主屏壁纸
	}

	private void DownloadSecondary()
	{
		LogHelper.WriteLogAsync("MeViewModel: 下载副屏壁纸");
		// TODO: 下载当前副屏壁纸
	}

	#endregion
}
