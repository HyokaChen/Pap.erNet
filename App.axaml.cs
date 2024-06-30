using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using HotAvalonia;
using Pap.erNet.ViewModels;
using Pap.erNet.Views;

namespace Pap.erNet;

public partial class App : Application
{
	public override void Initialize()
	{
		this.EnableHotReload();
		AvaloniaXamlLoader.Load(this);
	}

	public override void OnFrameworkInitializationCompleted()
	{
		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			var vm = new MainWindowViewModel();
			var mw = new MainWindow { DataContext = vm, ShowInTaskbar = false };
			desktop.MainWindow = mw;
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

	private void Exit(object? sender, System.EventArgs e)
	{
		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			desktop.Shutdown();
		}
	}
}
