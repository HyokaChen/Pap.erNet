// Copyright (c) 2024 Pap.erNet. All rights reserved.
// 功能：设置页面视图模型，管理随机壁纸、壁纸库、个性化选项及通用设置的状态与命令
// 作者：Pap.erNet 团队

using System.Windows.Input;
using Pap.erNet.Utils;
using ReactiveUI;

namespace Pap.erNet.ViewModels;

/// <summary>
/// 设置页面视图模型
/// 职责：封装设置页面的所有状态与业务逻辑，通过数据绑定驱动 UI
/// </summary>
public class SettingsViewModel : ViewModelBase
{
	#region 随机壁纸

	private bool _autoChangeWallpaper;

	/// <summary>开启自动换壁纸</summary>
	public bool AutoChangeWallpaper
	{
		get => _autoChangeWallpaper;
		set => this.RaiseAndSetIfChanged(ref _autoChangeWallpaper, value);
	}

	private string _selectedInterval = "每天";

	/// <summary>换壁纸间隔</summary>
	public string SelectedInterval
	{
		get => _selectedInterval;
		set => this.RaiseAndSetIfChanged(ref _selectedInterval, value);
	}

	/// <summary>可选间隔列表</summary>
	public string[] IntervalOptions { get; } = ["每天", "每周", "每月", "每小时"];

	private bool _isOnlineSource = true;

	/// <summary>壁纸来源（true=在线，false=壁纸库）</summary>
	public bool IsOnlineSource
	{
		get => _isOnlineSource;
		set => this.RaiseAndSetIfChanged(ref _isOnlineSource, value);
	}

	private bool _mirrorDisplays;

	/// <summary>主屏幕与外接屏幕相同壁纸</summary>
	public bool MirrorDisplays
	{
		get => _mirrorDisplays;
		set => this.RaiseAndSetIfChanged(ref _mirrorDisplays, value);
	}

	#endregion

	#region 壁纸库

	private string _wallpaperPath = "~/Pictures/pap.er";

	/// <summary>壁纸库路径</summary>
	public string WallpaperPath
	{
		get => _wallpaperPath;
		set => this.RaiseAndSetIfChanged(ref _wallpaperPath, value);
	}

	private string _storageSize = "37.84 MB";

	/// <summary>壁纸库占用空间</summary>
	public string StorageSize
	{
		get => _storageSize;
		set => this.RaiseAndSetIfChanged(ref _storageSize, value);
	}

	private bool _autoImport = true;

	/// <summary>自动导入目录内图片</summary>
	public bool AutoImport
	{
		get => _autoImport;
		set => this.RaiseAndSetIfChanged(ref _autoImport, value);
	}

	#endregion

	#region 个性化选项

	private bool _sameWallpaperAllSpaces;

	/// <summary>所有桌面空间使用相同壁纸</summary>
	public bool SameWallpaperAllSpaces
	{
		get => _sameWallpaperAllSpaces;
		set => this.RaiseAndSetIfChanged(ref _sameWallpaperAllSpaces, value);
	}

	private bool _dimMenuBar;

	/// <summary>将菜单栏调暗</summary>
	public bool DimMenuBar
	{
		get => _dimMenuBar;
		set => this.RaiseAndSetIfChanged(ref _dimMenuBar, value);
	}

	#endregion

	#region 通用

	private string _selectedLanguage = "简体中文";

	/// <summary>语言选择</summary>
	public string SelectedLanguage
	{
		get => _selectedLanguage;
		set => this.RaiseAndSetIfChanged(ref _selectedLanguage, value);
	}

	/// <summary>可选语言列表</summary>
	public string[] LanguageOptions { get; } = ["简体中文", "English", "日本語"];

	private bool _launchAtLogin = true;

	/// <summary>随系统启动</summary>
	public bool LaunchAtLogin
	{
		get => _launchAtLogin;
		set => this.RaiseAndSetIfChanged(ref _launchAtLogin, value);
	}

	private bool _showInDock = true;

	/// <summary>在程序坞显示图标</summary>
	public bool ShowInDock
	{
		get => _showInDock;
		set => this.RaiseAndSetIfChanged(ref _showInDock, value);
	}

	/// <summary>应用版本号</summary>
	public string Version => $"v{AppConstants.APP_VERSION}";

	#endregion

	#region 命令

	/// <summary>返回首页</summary>
	public ICommand NavigateBackCommand { get; }

	/// <summary>退出应用</summary>
	public ICommand QuitAppCommand { get; }

	/// <summary>选择壁纸库路径</summary>
	public ICommand SelectWallpaperPathCommand { get; }

	/// <summary>检查更新</summary>
	public ICommand CheckUpdateCommand { get; }

	/// <summary>切换壁纸来源到在线</summary>
	public ICommand SwitchToOnlineSourceCommand { get; }

	/// <summary>切换壁纸来源到壁纸库</summary>
	public ICommand SwitchToLocalSourceCommand { get; }

	// 各 Toggle 开关命令
	public ICommand ToggleAutoChangeWallpaperCommand { get; }
	public ICommand ToggleMirrorDisplaysCommand { get; }
	public ICommand ToggleAutoImportCommand { get; }
	public ICommand ToggleSameWallpaperCommand { get; }
	public ICommand ToggleDimMenuBarCommand { get; }
	public ICommand ToggleLaunchAtLoginCommand { get; }
	public ICommand ToggleShowInDockCommand { get; }

	#endregion

	public SettingsViewModel()
	{
		NavigateBackCommand = ReactiveCommand.Create(NavigateBack);
		QuitAppCommand = ReactiveCommand.Create(QuitApp);
		SelectWallpaperPathCommand = ReactiveCommand.Create(SelectWallpaperPath);
		CheckUpdateCommand = ReactiveCommand.Create(CheckUpdate);
		SwitchToOnlineSourceCommand = ReactiveCommand.Create(() => IsOnlineSource = true);
		SwitchToLocalSourceCommand = ReactiveCommand.Create(() => IsOnlineSource = false);

		// Toggle 命令：翻转对应的 bool 属性
		ToggleAutoChangeWallpaperCommand = ReactiveCommand.Create(() => AutoChangeWallpaper = !AutoChangeWallpaper);
		ToggleMirrorDisplaysCommand = ReactiveCommand.Create(() => MirrorDisplays = !MirrorDisplays);
		ToggleAutoImportCommand = ReactiveCommand.Create(() => AutoImport = !AutoImport);
		ToggleSameWallpaperCommand = ReactiveCommand.Create(() => SameWallpaperAllSpaces = !SameWallpaperAllSpaces);
		ToggleDimMenuBarCommand = ReactiveCommand.Create(() => DimMenuBar = !DimMenuBar);
		ToggleLaunchAtLoginCommand = ReactiveCommand.Create(() => LaunchAtLogin = !LaunchAtLogin);
		ToggleShowInDockCommand = ReactiveCommand.Create(() => ShowInDock = !ShowInDock);
	}

	/// <summary>请求导航回首页事件</summary>
	public event Action? NavigationBackRequested;

	/// <summary>请求退出应用事件</summary>
	public event Action? QuitRequested;

	#region 方法

	/// <summary>
	/// 返回首页导航
	/// </summary>
	private void NavigateBack()
	{
		LogHelper.WriteLogAsync("SettingsViewModel: 返回首页");
		NavigationBackRequested?.Invoke();
	}

	/// <summary>
	/// 退出应用
	/// </summary>
	private void QuitApp()
	{
		LogHelper.WriteLogAsync("SettingsViewModel: 退出应用");
		QuitRequested?.Invoke();
	}

	/// <summary>
	/// 选择壁纸库路径（打开文件夹选择器）
	/// </summary>
	private async void SelectWallpaperPath()
	{
		try
		{
			// TODO: 实现文件夹选择器
			LogHelper.WriteLogAsync("SettingsViewModel: 选择壁纸库路径");
			await Task.CompletedTask;
		}
		catch (Exception ex)
		{
			LogHelper.WriteLogAsync($"SettingsViewModel.SelectWallpaperPath 异常: {ex.Message}");
		}
	}

	/// <summary>
	/// 检查新版变化
	/// </summary>
	private static void CheckUpdate()
	{
		LogHelper.WriteLogAsync("SettingsViewModel: 检查更新");
		// TODO: 打开更新日志页面
	}

	#endregion
}
