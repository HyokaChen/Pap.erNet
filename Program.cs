using Avalonia;
using Avalonia.ReactiveUI;
using Pap.erNet.Utils;

namespace Pap.erNet;

sealed class Program
{
	// Initialization code. Don't use any Avalonia, third-party APIs or any
	// SynchronizationContext-reliant code before AppMain is called: things aren't initialized
	// yet and stuff might break.

	[STAThread]
	public static void Main(string[] args)
	{
		// 使用跨平台单例检测
		using var singleInstance = new SingleInstanceHelper("c270d6ef-7197-4150-99b9-b4d3d330f9a0");

		try
		{
			// 尝试获取单例锁
			if (!singleInstance.TryAcquireLock())
			{
				// 已有实例在运行，退出
				return;
			}

			var builder = BuildAvaloniaApp();
			builder.StartWithClassicDesktopLifetime(args, Avalonia.Controls.ShutdownMode.OnExplicitShutdown);
		}
		catch (Exception ex)
		{
			LogHelper.WriteLogAsync($"启动异常::{ex.Message}>>>{ex.StackTrace}");
			throw;
		}
	}

	// Avalonia configuration, don't remove; also used by visual designer.
	public static AppBuilder BuildAvaloniaApp() =>
		AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().LogToTrace().UseReactiveUI();
}
