using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using HotAvalonia;
using Microsoft.Extensions.DependencyInjection;
using Pap.erNet.Pages.Home;
using Pap.erNet.Utils;
using Pap.erNet.ViewModels;
using Pap.erNet.Views;

namespace Pap.erNet;

public partial class App : Application
{
	/// <summary>
	/// Gets the current <see cref="App"/> instance in use
	/// </summary>
	public static new App? Current => Application.Current as App;

	/// <summary>
	/// Gets the <see cref="IServiceProvider"/> instance to resolve application services.
	/// </summary>
	public IServiceProvider ServicesProvider { get; } = ConfigureServices();

	public override void Initialize()
	{
		this.UseHotReload();
		AvaloniaXamlLoader.Load(this);
	}

	public override void OnFrameworkInitializationCompleted()
	{
		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			// 在创建主窗口之前，先执行认证流程
			Task.Run(async () =>
				{
					try
					{
						var authResult = await AuthService.Instance.AuthenticateAsync();
						LogHelper.WriteLogAsync($"App: 认证结果 = {authResult}");
					}
					catch (Exception ex)
					{
						LogHelper.WriteLogAsync($"App: 认证异常 = {ex.Message}");
					}
				})
				.Wait(); // 同步等待认证完成，确保后续请求带有 Token

			desktop.MainWindow = ServicesProvider.GetRequiredService<MainWindow>();
		}

		base.OnFrameworkInitializationCompleted();
	}

	private void ShowOrHide(object? sender, EventArgs e)
	{
		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			if (desktop.MainWindow!.IsVisible)
			{
				desktop.MainWindow.Hide();
			}
			else if (!desktop.MainWindow.IsVisible)
			{
				desktop.MainWindow.Show();
			}
		}
	}

	private static IServiceProvider ConfigureServices()
	{
		var services = new ServiceCollection();

		services.AddSingleton<MainWindow>();
		services.AddTransient<WallpaperView>();
		services.AddTransient<WallpaperListView>();

		services.AddTransient<MainWindowViewModel>();

		return services.BuildServiceProvider();
	}

	private void Exit(object? sender, EventArgs e)
	{
		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			desktop.Shutdown();
		}
	}
}
