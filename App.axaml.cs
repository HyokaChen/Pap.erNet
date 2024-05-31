using System;
using Avalonia;
using Avalonia.Controls;
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
            var vm = new MainWindowViewModel { IsShowWallpaperWindow = false };
            desktop.MainWindow = new MainWindow { DataContext = vm, };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void TabChange(object? sender, SelectionChangedEventArgs e)
    {
        if (this.DataContext is MainWindowViewModel dataContext)
            dataContext.WallpaperListViewModel.LoadWallpapers();
    }
}
