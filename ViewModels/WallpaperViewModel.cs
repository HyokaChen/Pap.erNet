using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;
using Avalonia.Media;
using Pap.erNet.Models;
using Pap.erNet.Services;
using ReactiveUI;

namespace Pap.erNet.ViewModels;

public class WallpaperViewModel : ViewModelBase
{
	// 壁纸主色解析缓存（hex / rgb），避免每个 VM 重复解析
	private static readonly ConcurrentDictionary<string, Color?> ColorCache = new();
	private readonly Wallpaper _wallpaper;

	public WallpaperViewModel(Wallpaper wallpaper)
	{
		_wallpaper = wallpaper;

		// 首屏列表已秒出（Thumbnail 为空），缩略图后台异步编码，完成后通知绑定刷新
		if (string.IsNullOrEmpty(wallpaper.Thumbnail))
		{
			_ = LoadThumbnailAsync();
		}
	}

	/// <summary>
	/// 后台编码 blurhash 缩略图（Service 层已缓存编码结果），完成后通知 UI 立即显示
	/// </summary>
	private async Task LoadThumbnailAsync()
	{
		var thumbnail = await WallpaperListService.EncodeThumbnailAsync(_wallpaper.Blurhash).ConfigureAwait(false);
		if (string.IsNullOrEmpty(thumbnail))
			return;

		_wallpaper.Thumbnail = thumbnail;
		this.RaisePropertyChanged(nameof(ThumbnailSource));
	}

	public ICommand VisitBrowserCommand { get; } =
		ReactiveCommand.Create(
			(string url) =>
			{
				if (RuntimeInformation.IsOSPlatform((OSPlatform.Linux)))
				{
					ShellExec($"xdg-open {url}", waitForExit: false);
				}
				else
				{
					using var process = Process.Start(
						new ProcessStartInfo
						{
							FileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? url : "open",
							Arguments = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? $"{url}" : "",
							CreateNoWindow = true,
							UseShellExecute = RuntimeInformation.IsOSPlatform(OSPlatform.Windows),
						}
					);
				}
			}
		);

	public string ThumbnailSource => _wallpaper.Thumbnail;
	public string ImageSource => _wallpaper.Url;

	public bool IsLoad
	{
		get;
		set => this.RaiseAndSetIfChanged(ref field, value);
	}

	public string Link => _wallpaper.Link;
	public string ResolutionRatio => _wallpaper.ResolutionRatio;
	public string Author => _wallpaper.Author;

	/// <summary>
	/// 壁纸主色调画刷：列表加载瞬间（真实图/缩略图未就绪时）作为 Image 背景，
	/// 渐进增强（色块 → blurhash → 真实图），避免切换时空白闪烁
	/// </summary>
	public IBrush? ColorBrush
	{
		get
		{
			var color = ColorCache.GetOrAdd(
				_wallpaper.Color,
				static c =>
				{
					if (string.IsNullOrEmpty(c))
						return null;
					try
					{
						if (c.StartsWith('#'))
							return Color.Parse(c);

						// rgb(r, g, b) 格式兼容
						if (c.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
						{
							var start = c.IndexOf('(') + 1;
							var end = c.IndexOf(')');
							if (start > 0 && end > start)
							{
								var parts = c[start..end].Split(',', ' ');
								if (
									parts.Length >= 3
									&& byte.TryParse(parts[0], out var r)
									&& byte.TryParse(parts[1], out var g)
									&& byte.TryParse(parts[2], out var b)
								)
								{
									return new Color(255, r, g, b);
								}
							}
						}

						return Color.Parse(c);
					}
					catch
					{
						return null;
					}
				}
			);
			return color.HasValue ? new SolidColorBrush(color.Value) : null;
		}
	}

	private static void ShellExec(string cmd, bool waitForExit = true)
	{
		var escapeArgs = cmd.Replace("\"", "\\\"");
		using var process = Process.Start(
			new ProcessStartInfo
			{
				FileName = "/bin/sh",
				Arguments = $"-c \"{escapeArgs}\"",
				RedirectStandardOutput = true,
				UseShellExecute = false,
				CreateNoWindow = true,
				WindowStyle = ProcessWindowStyle.Hidden,
			}
		);
		if (waitForExit)
			process!.WaitForExit();
	}
}
