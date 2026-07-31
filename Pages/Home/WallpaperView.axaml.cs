using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Pap.erNet.Utils;
using Pap.erNet.ViewModels;

namespace Pap.erNet.Pages.Home;

public partial class WallpaperView : UserControl
{
	[LibraryImport("user32.dll", EntryPoint = "SystemParametersInfoW", StringMarshalling = StringMarshalling.Utf16)]
	private static partial int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);

	private const int SPI_SETDESKWALLPAPER = 20;
	private const int SPIF_UPDATEINIFILE = 0x1;

	public WallpaperView()
	{
		InitializeComponent();
	}

	private void GotEnter(object? sender, PointerEventArgs e)
	{
		Author.IsVisible = true;
		ResolutionRatio.IsVisible = true;
		SetDeskWallpaper.IsVisible = true;
	}

	private void GotExit(object? sender, PointerEventArgs e)
	{
		Author.IsVisible = false;
		ResolutionRatio.IsVisible = false;
		SetDeskWallpaper.IsVisible = false;
	}

	private void SetDeskWallpaper_PointerEntered(object? sender, PointerEventArgs e)
	{
		SetDeskWallpaper.Opacity = 0.8;
	}

	private void SetDeskWallpaper_PointerExited(object? sender, PointerEventArgs e)
	{
		SetDeskWallpaper.Opacity = 0.5;
	}

	private async void SetDeskWallpaper_PointerPressed(object? sender, PointerPressedEventArgs e)
	{
		try
		{
			DownloadPB.IsVisible = true;
			var vm = DataContext as WallpaperViewModel;
			var fileName = vm.ImageSource.Split("/")[^2];
			var filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), fileName);
			if (!File.Exists(filePath))
			{
				var fullUrl = vm.ImageSource.Replace("/thumb", "/full");
				await DownloadAsync(fullUrl, filePath);
			}

			// 跨平台设置桌面壁纸
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				await SetWindowsWallpaperAsync(filePath);
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			{
				await SetLinuxWallpaper(filePath);
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
			{
				await SetMacWallpaperAsync(filePath);
			}

			// 进度条至少显示 600ms，避免下载太快闪一下就消失
			await Task.Delay(600);
			DownloadPB.IsVisible = false;
		}
		catch (Exception ex)
		{
			LogHelper.WriteLogAsync($"设置桌面壁纸失败:{ex.GetType().Name}:{ex.Message}");
		}
	}

	private async Task DownloadAsync(string fullUrl, string filePath)
	{
		try
		{
			using var client = new HttpClient(
				new SocketsHttpHandler
				{
					UseProxy = false,
					MaxConnectionsPerServer = 5,
					AllowAutoRedirect = true,
					SslOptions = new SslClientAuthenticationOptions
					{
						RemoteCertificateValidationCallback = (_, _, _, _) => true,
						EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
					},
				}
			);
			client.Timeout = TimeSpan.FromSeconds(300);
			client.DefaultRequestHeaders.UserAgent.ParseAdd(DeviceUtil.GetImageDownloadUserAgent());
			client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("image/avif"));
			client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("image/webp"));
			client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*") { Quality = 0.9 });
			client.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
			client.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("deflate"));

			LogHelper.WriteLogAsync($"Download Url::{fullUrl}");

			using var response = await client.GetAsync(fullUrl, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);

			var totalLen = response.Content.Headers.ContentLength.GetValueOrDefault(-1);

			var dir = Path.GetDirectoryName(filePath);
			if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
				Directory.CreateDirectory(dir);

			await using var downloadFile = File.Create(filePath);
			await using var download = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);

			// 把整个下载循环丢到后台线程，UI 线程完全自由
			await Task.Run(async () =>
			{
				var buffer = new byte[8192];
				long totalBytesRead = 0;
				int bytesRead;

				while ((bytesRead = await download.ReadAsync(buffer).ConfigureAwait(false)) != 0)
				{
					await downloadFile.WriteAsync(buffer.AsMemory(0, bytesRead)).ConfigureAwait(false);
					totalBytesRead += bytesRead;

					// 每次读取都更新进度条，流式展示
					var progress = totalLen > 0 ? totalBytesRead * 100.0 / totalLen : 0;
					await Dispatcher.UIThread.InvokeAsync(
						() =>
						{
							DownloadPB.Value = progress;
						},
						DispatcherPriority.Background
					);
				}

				LogHelper.WriteLogAsync($"下载完成: {fullUrl}, 大小={totalBytesRead}");
			});
		}
		catch (Exception ex)
		{
			LogHelper.WriteLogAsync($"下载失败:{ex.GetType().Name}:{ex.Message}>>>{ex.StackTrace}");
			try
			{
				File.Delete(filePath);
			}
			catch
			{ /* ignore */
			}
		}
	}

	private static async Task SetWindowsWallpaperAsync(string filePath)
	{
		await Task.Run(() => SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, filePath, SPIF_UPDATEINIFILE));
	}

	private static async Task SetMacWallpaperAsync(string filePath)
	{
		try
		{
			var absolutePath = Path.GetFullPath(filePath);
			using var process = Process.Start(
				new ProcessStartInfo
				{
					FileName = "/usr/bin/osascript",
					Arguments =
						$"-e \"tell application \\\"System Events\\\" to set picture of every desktop to POSIX file \\\"{absolutePath}\\\"\"",
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					UseShellExecute = false,
					CreateNoWindow = true,
				}
			);
			if (process is null)
			{
				LogHelper.WriteLogAsync("设置Mac壁纸失败: 无法启动 osascript 进程");
				return;
			}

			using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
			await process.WaitForExitAsync(cts.Token);

			var error = await process.StandardError.ReadToEndAsync(cts.Token);
			LogHelper.WriteLogAsync(!string.IsNullOrWhiteSpace(error) ? $"设置Mac壁纸失败: {error}" : $"Mac壁纸设置成功: {absolutePath}");
		}
		catch (OperationCanceledException)
		{
			LogHelper.WriteLogAsync("设置Mac壁纸超时(5秒)");
		}
		catch (Exception ex)
		{
			LogHelper.WriteLogAsync($"设置Mac壁纸异常: {ex.Message}");
		}
	}

	private async Task SetLinuxWallpaper(string filePath)
	{
		try
		{
			// 确保文件路径是绝对路径
			var absolutePath = Path.GetFullPath(filePath);

			// 检测当前桌面环境
			var desktopEnv = GetLinuxDesktopEnvironment();
			var success = false;
			var errorMsg = string.Empty;

			LogHelper.WriteLogAsync($"检测到桌面环境: {desktopEnv}，壁纸路径: {absolutePath}");

			switch (desktopEnv)
			{
				case "gnome":
				case "ubuntu":
				case "unity":
				{
					var resStatus = await SetGnomeWallpaper(absolutePath);
					success = resStatus.Success;
					errorMsg = resStatus.ErrorMessage;
					break;
				}
				case "kde":
				case "plasma":
				{
					var resStatus = await SetKdeWallpaper(absolutePath);
					success = resStatus.Success;
					errorMsg = resStatus.ErrorMessage;
					break;
				}
				case "xfce":
				{
					var resStatus = await SetXfceWallpaper(absolutePath);
					success = resStatus.Success;
					errorMsg = resStatus.ErrorMessage;
					break;
				}
				case "cinnamon":
				{
					var resStatus = await SetCinnamonWallpaper(absolutePath);
					success = resStatus.Success;
					errorMsg = resStatus.ErrorMessage;
					break;
				}
				case "mate":
				{
					var resStatus = await SetMateWallpaper(absolutePath);
					success = resStatus.Success;
					errorMsg = resStatus.ErrorMessage;
					break;
				}
				default:
				{
					// 尝试通用方法
					var resStatus = await TryGenericWallpaperMethods(absolutePath);
					success = resStatus.Success;
					errorMsg = resStatus.ErrorMessage;
					break;
				}
			}

			LogHelper.WriteLogAsync(
				success ? $"Linux壁纸设置成功，桌面环境: {desktopEnv}" : $"Linux壁纸设置失败，桌面环境: {desktopEnv}，错误: {errorMsg}"
			);
		}
		catch (Exception ex)
		{
			LogHelper.WriteLogAsync($"Linux壁纸设置出现报错: {ex.Message}>>>{ex.StackTrace}");
		}
	}

	private static string GetLinuxDesktopEnvironment()
	{
		// 尝试从环境变量获取桌面环境
		var desktopSession = Environment.GetEnvironmentVariable("DESKTOP_SESSION")?.ToLower() ?? "";
		var xdgCurrentDesktop = Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP")?.ToLower() ?? "";
		var gdmSession = Environment.GetEnvironmentVariable("GDMSESSION")?.ToLower() ?? "";

		// 优先检查 XDG_CURRENT_DESKTOP
		if (!string.IsNullOrEmpty(xdgCurrentDesktop))
		{
			if (xdgCurrentDesktop.Contains("gnome"))
				return "gnome";
			if (xdgCurrentDesktop.Contains("kde") || xdgCurrentDesktop.Contains("plasma"))
				return "kde";
			if (xdgCurrentDesktop.Contains("xfce"))
				return "xfce";
			if (xdgCurrentDesktop.Contains("cinnamon") || xdgCurrentDesktop.Contains("x-cinnamon"))
				return "cinnamon";
			if (xdgCurrentDesktop.Contains("mate"))
				return "mate";
			if (xdgCurrentDesktop.Contains("unity"))
				return "unity";
		}

		// 检查 DESKTOP_SESSION
		if (!string.IsNullOrEmpty(desktopSession))
		{
			if (desktopSession.Contains("gnome") || desktopSession.Contains("ubuntu"))
				return "gnome";
			if (desktopSession.Contains("kde") || desktopSession.Contains("plasma"))
				return "kde";
			if (desktopSession.Contains("xfce"))
				return "xfce";
			if (desktopSession.Contains("cinnamon"))
				return "cinnamon";
			if (desktopSession.Contains("mate"))
				return "mate";
			if (desktopSession.Contains("unity"))
				return "unity";
		}

		// 检查 GDMSESSION
		if (!string.IsNullOrEmpty(gdmSession))
		{
			if (gdmSession.Contains("gnome") || gdmSession.Contains("ubuntu"))
				return "gnome";
			if (gdmSession.Contains("kde") || gdmSession.Contains("plasma"))
				return "kde";
			if (gdmSession.Contains("xfce"))
				return "xfce";
			if (gdmSession.Contains("cinnamon"))
				return "cinnamon";
			if (gdmSession.Contains("mate"))
				return "mate";
		}

		return "unknown";
	}

	private async Task<(bool Success, string ErrorMessage)> SetGnomeWallpaper(string filePath)
	{
		try
		{
			// 构建正确的 file:// URI
			var fileUri = $"file://{filePath}";

			// 设置浅色模式壁纸
			var processStartInfo1 = new ProcessStartInfo
			{
				FileName = "gsettings",
				Arguments = $"set org.gnome.desktop.background picture-uri '{fileUri}'",
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
			};

			using var process1 = Process.Start(processStartInfo1);
			await process1!.WaitForExitAsync();
			var errorMsg1 = await process1.StandardError.ReadToEndAsync();

			// 设置深色模式壁纸（GNOME 42+）
			var processStartInfo2 = new ProcessStartInfo
			{
				FileName = "gsettings",
				Arguments = $"set org.gnome.desktop.background picture-uri-dark '{fileUri}'",
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
			};

			using var process2 = Process.Start(processStartInfo2);
			await process2!.WaitForExitAsync();
			var errorMsg2 = await process2.StandardError.ReadToEndAsync();

			// 设置壁纸选项为缩放
			var processStartInfo3 = new ProcessStartInfo
			{
				FileName = "gsettings",
				Arguments = "set org.gnome.desktop.background picture-options 'zoom'",
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
			};

			using var process3 = Process.Start(processStartInfo3);
			await process3!.WaitForExitAsync();

			var success = process1.ExitCode == 0;
			var errorMsg = success ? "" : errorMsg1 + errorMsg2;

			return (success, errorMsg);
		}
		catch (Exception ex)
		{
			return (false, ex.Message);
		}
	}

	private static async Task<(bool Success, string ErrorMessage)> SetKdeWallpaper(string filePath)
	{
		try
		{
			// 方法1: 尝试使用 plasma-apply-wallpaperimage (Plasma 5.18+)
			var processStartInfo1 = new ProcessStartInfo
			{
				FileName = "plasma-apply-wallpaperimage",
				Arguments = $"\"{filePath}\"",
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
			};

			try
			{
				using var process1 = Process.Start(processStartInfo1);
				if (process1 != null)
				{
					await process1.WaitForExitAsync();
					if (process1.ExitCode == 0)
					{
						return (true, "");
					}
				}
			}
			catch
			{
				// plasma-apply-wallpaperimage 不存在，尝试其他方法
			}

			// 方法2: 使用 qdbus/qdbus-qt5 调用 Plasma Shell
			var qdbusCommands = new[] { "qdbus-qt5", "qdbus", "qdbus6" };
			foreach (var qdbusCmd in qdbusCommands)
			{
				try
				{
					// 构建 JavaScript 脚本来设置壁纸
					var script =
						$@"
var allDesktops = desktops();
for (i=0;i<allDesktops.length;i++) {{
    d = allDesktops[i];
    d.wallpaperPlugin = ""org.kde.image"";
    d.currentConfigGroup = Array(""Wallpaper"", ""org.kde.image"", ""General"");
    d.writeConfig(""Image"", ""file://{filePath}"");
}}
";

					var processStartInfo2 = new ProcessStartInfo
					{
						FileName = qdbusCmd,
						Arguments =
							$"org.kde.plasmashell /PlasmaShell org.kde.PlasmaShell.evaluateScript '{script.Replace("\n", " ").Replace("\r", "")}'",
						UseShellExecute = false,
						CreateNoWindow = true,
						RedirectStandardOutput = true,
						RedirectStandardError = true,
					};

					using var process2 = Process.Start(processStartInfo2);
					if (process2 != null)
					{
						await process2.WaitForExitAsync();
						if (process2.ExitCode == 0)
						{
							return (true, "");
						}
					}
				}
				catch
				{
					// ignored
				}
			}

			return (false, "无法找到可用的 KDE 壁纸设置命令");
		}
		catch (Exception ex)
		{
			return (false, ex.Message);
		}
	}

	private static async Task<(bool Success, string ErrorMessage)> SetXfceWallpaper(string filePath)
	{
		try
		{
			// XFCE 使用 xfconf-query 设置壁纸
			var processStartInfo = new ProcessStartInfo
			{
				FileName = "xfconf-query",
				Arguments = $"-c xfce4-desktop -p /backdrop/screen0/monitor0/workspace0/last-image -s \"{filePath}\"",
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
			};

			using var process = Process.Start(processStartInfo);
			await process!.WaitForExitAsync();
			var errorMsg = await process.StandardError.ReadToEndAsync();

			return (process.ExitCode == 0, errorMsg);
		}
		catch (Exception ex)
		{
			return (false, ex.Message);
		}
	}

	private static async Task<(bool Success, string ErrorMessage)> SetCinnamonWallpaper(string filePath)
	{
		try
		{
			// Cinnamon 使用 gsettings
			var fileUri = $"file://{filePath}";
			var processStartInfo = new ProcessStartInfo
			{
				FileName = "gsettings",
				Arguments = $"set org.cinnamon.desktop.background picture-uri '{fileUri}'",
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
			};

			using var process = Process.Start(processStartInfo);
			await process!.WaitForExitAsync();
			var errorMsg = await process.StandardError.ReadToEndAsync();

			return (process.ExitCode == 0, errorMsg);
		}
		catch (Exception ex)
		{
			return (false, ex.Message);
		}
	}

	private static async Task<(bool Success, string ErrorMessage)> SetMateWallpaper(string filePath)
	{
		try
		{
			// MATE 使用 gsettings
			var processStartInfo = new ProcessStartInfo
			{
				FileName = "gsettings",
				Arguments = $"set org.mate.background picture-filename \"{filePath}\"",
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
			};

			using var process = Process.Start(processStartInfo);
			await process!.WaitForExitAsync();
			var errorMsg = await process.StandardError.ReadToEndAsync();

			return (process.ExitCode == 0, errorMsg);
		}
		catch (Exception ex)
		{
			return (false, ex.Message);
		}
	}

	private static async Task<(bool Success, string ErrorMessage)> TryGenericWallpaperMethods(string filePath)
	{
		try
		{
			// 尝试通用方法：依次尝试各种可能的命令
			var methods = new[]
			{
				("gsettings", $"set org.gnome.desktop.background picture-uri 'file://{filePath}'"),
				("gsettings", $"set org.cinnamon.desktop.background picture-uri 'file://{filePath}'"),
				("gsettings", $"set org.mate.background picture-filename \"{filePath}\""),
				("xfconf-query", $"-c xfce4-desktop -p /backdrop/screen0/monitor0/workspace0/last-image -s \"{filePath}\""),
				("feh", $"--bg-scale \"{filePath}\""),
				("nitrogen", $"--set-zoom-fill \"{filePath}\""),
			};

			foreach (var (command, arguments) in methods)
			{
				try
				{
					var processStartInfo = new ProcessStartInfo
					{
						FileName = command,
						Arguments = arguments,
						UseShellExecute = false,
						CreateNoWindow = true,
						RedirectStandardOutput = true,
						RedirectStandardError = true,
					};

					using var process = Process.Start(processStartInfo);
					if (process != null)
					{
						await process.WaitForExitAsync();
						if (process.ExitCode == 0)
						{
							return (true, $"使用 {command} 设置成功");
						}
					}
				}
				catch
				{
					// ignored
				}
			}

			return (false, "所有通用方法均失败");
		}
		catch (Exception ex)
		{
			return (false, ex.Message);
		}
	}
}
