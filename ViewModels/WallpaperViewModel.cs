using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;
using Pap.erNet.Models;
using ReactiveUI;

namespace Pap.erNet.ViewModels;

public class WallpaperViewModel(Wallpaper wallpaper) : ViewModelBase
{
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
						new ProcessStartInfo()
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

	public string ThumbnailSource => wallpaper.Thumbnail;
	public string ImageSource => wallpaper.Url;
	public string Id => wallpaper.Id;

	private bool _isLoad = false;

	public bool IsLoad
	{
		get => _isLoad;
		set => this.RaiseAndSetIfChanged(ref _isLoad, value);
	}

	public string Link => wallpaper.Link;
	public string ResolutionRatio => wallpaper.ResolutionRatio;
	public string Author => wallpaper.Author;

	private static void ShellExec(string cmd, bool waitForExit = true)
	{
		var escapeArgs = cmd.Replace("\"", "\\\"");
		using var process = Process.Start(
			new ProcessStartInfo()
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
