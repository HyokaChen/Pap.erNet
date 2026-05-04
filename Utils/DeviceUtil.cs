using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace Pap.erNet.Utils;

/// <summary>
/// 设备唯一标识与设备信息工具类
/// Windows: CPU ID + BIOS Serial Number 的 MD5
/// Linux: /etc/machine-id
/// </summary>
public static class DeviceUtil
{
	private static string? _cachedUid;

	/// <summary>
	/// 获取设备唯一标识 (did/uid)
	/// 结果会被缓存，多次调用返回同一值
	/// </summary>
	public static string GetDeviceUid()
	{
		if (_cachedUid != null)
			return _cachedUid;

		_cachedUid = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? GetWindowsUid() : GetLinuxUid();

		return _cachedUid;
	}

	/// <summary>
	/// 获取操作系统名称
	/// </summary>
	public static string GetOsName()
	{
		return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Windows" : "Linux";
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
		return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? GetWindowsDeviceModel() : "Linux PC";
	}

	/// <summary>
	/// 获取屏幕分辨率列表
	/// 格式: "x,y,width,height"
	/// </summary>
	public static List<string> GetScreens()
	{
		var screens = new List<string>();
		try
		{
			// 使用 Avalonia 获取屏幕信息需要在 UI 线程上执行
			// 这里提供一个简单的实现，实际使用时可能需要调整
			screens.Add("0.0,0.0,1920.0,1080.0");
		}
		catch (Exception ex)
		{
			LogHelper.WriteLogAsync($"获取屏幕信息失败: {ex.Message}");
		}
		return screens;
	}

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
			// 降级方案：使用机器名 + 用户名的 MD5
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

	/// <summary>
	/// 计算字符串的 MD5 哈希值（十六进制小写输出）
	/// </summary>
	private static string ComputeMd5(string input)
	{
		var bytes = MD5.HashData(Encoding.UTF8.GetBytes(input));
		return Convert.ToHexString(bytes).ToLowerInvariant();
	}

	/// <summary>
	/// 执行系统命令并返回输出
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

			// Linux 需要通过 shell 执行管道命令
			if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
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

	#endregion
}
