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
		try
		{
			var mutex = new Mutex(true, "c270d6ef-7197-4150-99b9-b4d3d330f9a0", out bool mutexResult);

			if (!mutexResult)
			{
				return;
			}
			var builder = BuildAvaloniaApp();
			builder.StartWithClassicDesktopLifetime(args, Avalonia.Controls.ShutdownMode.OnExplicitShutdown);
			mutex.ReleaseMutex();
		}
		catch (Exception ex)
		{
			LogHelper.WriteLog($"启动异常::{ex.Message}>>>{ex.StackTrace}");
		}
	}

	// Avalonia configuration, don't remove; also used by visual designer.
	public static AppBuilder BuildAvaloniaApp() =>
		AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().LogToTrace().UseReactiveUI();
}
