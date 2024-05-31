using System;
using System.Reactive;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
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
            InputElement.LostFocusEvent.Raised.Subscribe(
                new AnonymousObserver<(object, Avalonia.Interactivity.RoutedEventArgs)>(
                    (s) =>
                    {
                        var sender = s.Item1;
                        if (sender is MainWindow mainWindow)
                        {
                            mainWindow.Hide();
                        }
                    }
                )
            );
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void TabChange(object? sender, SelectionChangedEventArgs e)
    {
        if (this.DataContext is MainWindowViewModel dataContext)
            dataContext.WallpaperListViewModel.LoadWallpapers();
    }

    private void TrayIcon_Clicked(object? sender, System.EventArgs e)
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
}
