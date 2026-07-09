using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Pap.erNet.Utils;
using Pap.erNet.ViewModels;

namespace Pap.erNet.Pages.Home;

public partial class WallpaperView : UserControl
{
	[DllImport("user32.dll", CharSet = CharSet.Auto)]
	private static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);

	private const int SPI_SETDESKWALLPAPER = 20;
	private const int SPIF_UPDATEINIFILE = 0x1;

	public WallpaperView()
	{
		InitializeComponent();
	}

	/// <summary>
	/// 鼠标进入卡片，显示额外信息
	/// </summary>
	private void GotEnter(object? sender, PointerEventArgs e)
	{
		QualityBadge.Opacity = 1;
		AuthorBtn.Opacity = 1;
		ResolutionBadge.Opacity = 1;
	}

	/// <summary>
	/// 鼠标离开卡片，隐藏额外信息
	/// </summary>
	private void GotExit(object? sender, PointerEventArgs e)
	{
		QualityBadge.Opacity = 0;
		AuthorBtn.Opacity = 0;
		ResolutionBadge.Opacity = 0;
	}

	private void ToolbarBtn_PointerEntered(object? sender, PointerEventArgs e)
	{
		if (sender is Button btn)
		{
			btn.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromArgb(255, 58, 58, 58));
		}
	}

	private void ToolbarBtn_PointerExited(object? sender, PointerEventArgs e)
	{
		if (sender is Button btn)
		{
			if (btn.Name is "SetDesktopBtn" or "SetDesktopDropdown")
			{
				btn.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromArgb(255, 50, 50, 50));
			}
			else
			{
				btn.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromArgb(0, 0, 0, 0));
			}
		}
	}

	private async void OnDownloadClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
	{
		try
		{
			DownloadPB.IsVisible = true;
			var vm = DataContext as WallpaperViewModel;
			if (vm == null)
				return;

			var fileName = vm.ImageSource.Split("/")[^2];
			var filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Pap.er", fileName);

			var dir = Path.GetDirectoryName(filePath);
			if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
			{
				Directory.CreateDirectory(dir);
			}

			if (!File.Exists(filePath))
			{
				var fullUrl = vm.ImageSource.Replace("/thumb", "/full");
				await DownloadAsync(fullUrl, filePath);
			}

			DownloadPB.IsVisible = false;
			LogHelper.WriteLogAsync($"壁纸下载完成: {filePath}");
		}
		catch (Exception ex)
		{
			LogHelper.WriteLogAsync($"下载失败: {ex.Message}");
			DownloadPB.IsVisible = false;
		}
	}

	private async Task DownloadAsync(string fullUrl, string filePath)
	{
		try
		{
			var client = new HttpClient(
				new SocketsHttpHandler
				{
					UseProxy = false,
					MaxConnectionsPerServer = 5,
					AllowAutoRedirect = true,
					SslOptions = new SslClientAuthenticationOptions { RemoteCertificateValidationCallback = (_, _, _, _) => true },
				}
			)
			{
				Timeout = TimeSpan.FromSeconds(300),
			};

			client.DefaultRequestHeaders.UserAgent.ParseAdd(DeviceUtil.GetImageDownloadUserAgent());
			client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("image/avif"));
			client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("image/webp"));
			client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*") { Quality = 0.9 });
			client.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
			client.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("deflate"));
			client.DefaultRequestHeaders.Host = "c3.wuse.co";

			LogHelper.WriteLogAsync($"Download Url: {fullUrl}");

			using var response = await client.GetAsync(fullUrl, HttpCompletionOption.ResponseHeadersRead);
			var totalLen = response.Content.Headers.ContentLength ?? -1;
			await using var downloadFile = File.Create(filePath);
			await using var download = await response.Content.ReadAsStreamAsync();

			var buffer = new byte[10240];
			long totalBytesRead = 0;
			int bytesRead;

			while ((bytesRead = await download.ReadAsync(buffer).ConfigureAwait(false)) != 0)
			{
				await downloadFile.WriteAsync(buffer.AsMemory(0, bytesRead)).ConfigureAwait(false);
				totalBytesRead += bytesRead;

				Dispatcher.UIThread.Invoke(() =>
				{
					DownloadPB.Value = totalBytesRead * 1.0 / totalLen * 100;
				});
			}
		}
		catch (Exception ex)
		{
			LogHelper.WriteLogAsync($"下载异常: {ex.Message}>>>{ex.StackTrace}");
		}
	}
}
