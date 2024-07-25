using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using HotAvalonia;
using Microsoft.Extensions.DependencyInjection;
using Pap.erNet.Pages.Home;
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
	public IServiceProvider ServicesProvider { get; }

	public App()
	{
		ServicesProvider = ConfigureServices();
	}

	public override void Initialize()
	{
		this.EnableHotReload();
		AvaloniaXamlLoader.Load(this);
	}

	public override void OnFrameworkInitializationCompleted()
	{
		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
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

	private void Exit(object? sender, System.EventArgs e)
	{
		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			desktop.Shutdown();
		}
	}
}
