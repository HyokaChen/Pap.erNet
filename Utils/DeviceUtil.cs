using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace Pap.erNet.Utils;

/// <summary>
/// 设备唯一标识与设备信息工具类
/// macOS: system_profiler 序列号 → MD5
/// Windows: CPU ID + BIOS Serial Number → MD5
/// Linux: /etc/machine-id → MD5
/// </summary>
public static class DeviceUtil
{
	private static string? _cachedUid;

	private static bool IsMacOS => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
	private static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
	private static bool IsLinux => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

	/// <summary>
	/// 获取设备唯一标识 (did/uid)
	/// </summary>
	public static string GetDeviceUid()
	{
		if (_cachedUid != null)
			return _cachedUid;

		if (IsMacOS)
			_cachedUid = GetMacUid();
		else if (IsWindows)
			_cachedUid = GetWindowsUid();
		else
			_cachedUid = GetLinuxUid();

		return _cachedUid;
	}

	/// <summary>
	/// 获取操作系统名称（匹配原始 pap.er 应用）
	/// </summary>
	public static string GetOsName()
	{
		if (IsMacOS)
			return "OS X";
		if (IsWindows)
			return "Windows";
		return "Linux";
	}

	/// <summary>
	/// 获取操作系统版本
	/// </summary>
	public static string GetOsVersion()
	{
		return Environment.OSVersion.VersionString;
	}

	/// <summary>
	/// 获取设备型号
	/// </summary>
	public static string GetDeviceModel()
	{
		if (IsMacOS)
			return GetMacDeviceModel();
		if (IsWindows)
			return GetWindowsDeviceModel();
		return "Linux PC";
	}

	/// <summary>
	/// 获取分发渠道编号（2=macOS, 3=Linux, 4=Windows）
	/// </summary>
	public static int GetDistribution()
	{
		if (IsMacOS)
			return 2;
		if (IsWindows)
			return 4;
		return 3;
	}

	/// <summary>
	/// GraphQL 请求使用的 User-Agent（模仿原始 pap.er 应用）
	/// </summary>
	public static string GetGraphQlUserAgent()
	{
		var build = AppConstants.APP_BUILD;
		if (IsMacOS)
		{
			var osVersion = Environment.OSVersion.Version;
			return $"pap.er/{build} CFNetwork/3860.500.112 Darwin/{osVersion.Major}.{osVersion.Minor}.{osVersion.Build}";
		}
		return $"pap.er/{build}";
	}

	/// <summary>
	/// 图片下载使用的 User-Agent（浏览器风格 Chrome 103 UA）
	/// </summary>
	public static string GetImageDownloadUserAgent()
	{
		if (IsMacOS)
			return "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/103.0.0.0 Safari/537.36";
		if (IsWindows)
			return "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/103.0.0.0 Safari/537.36";
		return "Mozilla/5.0 (Wayland; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/103.0.0.0 Safari/537.36";
	}

	/// <summary>
	/// 端点探测使用的 User-Agent（Alamofire 风格）
	/// </summary>
	public static string GetEndpointUserAgent()
	{
		var osName =
			IsMacOS ? "macOS"
			: IsWindows ? "Windows"
			: "Linux";
		var osVersion = Environment.OSVersion.Version;
		return $"pap.er/{AppConstants.APP_VERSION} (com.w.paper; build:{AppConstants.APP_BUILD}; {osName} {osVersion.Major}.{osVersion.Minor}.{osVersion.Build}) Alamofire/5.11.1";
	}

	/// <summary>
	/// 获取屏幕分辨率列表，格式: "x,y,width,height"
	/// </summary>
	public static List<string> GetScreens()
	{
		var screens = new List<string>();
		try
		{
			screens.Add("0.0,0.0,1920.0,1080.0");
		}
		catch (Exception ex)
		{
			LogHelper.WriteLogAsync($"获取屏幕信息失败: {ex.Message}");
		}
		return screens;
	}

	#region macOS

	private static string GetMacUid()
	{
		try
		{
			var output = RunCommandRaw("/usr/sbin/system_profiler", "SPHardwareDataType");
			foreach (var line in output.Split('\n'))
			{
				var trimmed = line.Trim();
				if (trimmed.StartsWith("Serial Number", StringComparison.OrdinalIgnoreCase))
				{
					var parts = trimmed.Split(':');
					if (parts.Length >= 2)
					{
						var serial = parts[1].Trim();
						if (!string.IsNullOrEmpty(serial))
							return ComputeMd5(serial);
					}
				}
			}
			throw new Exception("无法从 system_profiler 输出中找到序列号");
		}
		catch (Exception ex)
		{
			LogHelper.WriteLogAsync($"获取 macOS UID 失败: {ex.Message}");
			return ComputeMd5($"{Environment.MachineName}{Environment.UserName}");
		}
	}

	private static string GetMacDeviceModel()
	{
		try
		{
			var output = RunCommandRaw("/usr/sbin/sysctl", "-n hw.model");
			return output.Trim();
		}
		catch
		{
			return "Mac";
		}
	}

	#endregion

	#region Windows

	private static string GetWindowsUid()
	{
		try
		{
			var cpuId = RunCommand("wmic", "cpu get processorid").Replace("ProcessorId", "").Trim();
			var mbSerial = RunCommand("wmic", "bios get serialnumber").Replace("SerialNumber", "").Trim();
			var fingerprint = $"{cpuId}{mbSerial}";
			return ComputeMd5(fingerprint);
		}
		catch (Exception ex)
		{
			LogHelper.WriteLogAsync($"获取 Windows UID 失败: {ex.Message}");
			return ComputeMd5($"{Environment.MachineName}{Environment.UserName}");
		}
	}

	private static string GetWindowsDeviceModel()
	{
		try
		{
			var model = RunCommand("wmic", "computersystem get model").Replace("Model", "").Trim();
			return string.IsNullOrEmpty(model) ? "Windows PC" : model;
		}
		catch
		{
			return "Windows PC";
		}
	}

	#endregion

	#region Linux

	private static string GetLinuxUid()
	{
		try
		{
			var machineId = File.ReadAllText("/etc/machine-id").Trim();
			if (!string.IsNullOrEmpty(machineId))
				return ComputeMd5(machineId);

			return ComputeMd5($"{Environment.MachineName}{Environment.UserName}");
		}
		catch (Exception ex)
		{
			LogHelper.WriteLogAsync($"获取 Linux UID 失败: {ex.Message}");
			return ComputeMd5($"{Environment.MachineName}{Environment.UserName}");
		}
	}

	#endregion

	#region 辅助方法

	private static string ComputeMd5(string input)
	{
		var bytes = MD5.HashData(Encoding.UTF8.GetBytes(input));
		return Convert.ToHexString(bytes).ToLowerInvariant();
	}

	/// <summary>
	/// 执行系统命令并返回输出（Windows 和 Linux 通过 shell）
	/// </summary>
	private static string RunCommand(string fileName, string arguments)
	{
		try
		{
			using var process = new Process();
			process.StartInfo = new ProcessStartInfo
			{
				FileName = fileName,
				Arguments = arguments,
				RedirectStandardOutput = true,
				UseShellExecute = false,
				CreateNoWindow = true,
				WindowStyle = ProcessWindowStyle.Hidden,
			};

			// Linux 通过 shell 执行
			if (IsLinux)
			{
				process.StartInfo.FileName = "/bin/sh";
				process.StartInfo.Arguments = $"-c \"{fileName} {arguments}\"";
			}

			process.Start();
			var output = process.StandardOutput.ReadToEnd();
			process.WaitForExit();
			return output;
		}
		catch (Exception ex)
		{
			LogHelper.WriteLogAsync($"执行命令失败: {fileName} {arguments}, 错误: {ex.Message}");
			return string.Empty;
		}
	}

	/// <summary>
	/// 直接执行命令（不通过 shell 包装，适用于 macOS）
	/// </summary>
	private static string RunCommandRaw(string fileName, string arguments)
	{
		try
		{
			using var process = new Process();
			process.StartInfo = new ProcessStartInfo
			{
				FileName = fileName,
				Arguments = arguments,
				RedirectStandardOutput = true,
				UseShellExecute = false,
				CreateNoWindow = true,
			};

			process.Start();
			var output = process.StandardOutput.ReadToEnd();
			process.WaitForExit();
			return output;
		}
		catch (Exception ex)
		{
			LogHelper.WriteLogAsync($"执行命令失败: {fileName} {arguments}, 错误: {ex.Message}");
			return string.Empty;
		}
	}

	#endregion
}
