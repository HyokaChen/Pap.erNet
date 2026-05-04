using System.Text.Json.Serialization;

namespace Pap.erNet.Models;

/// <summary>
/// UpdateDevice / CheckinDevice 请求的 Variables
/// </summary>
public class DeviceVariables
{
	public string? Uid { get; set; }
	public string? AppVer { get; set; }
	public double? AppBuild { get; set; }
	public string? DeviceModel { get; set; }
	public string? OsName { get; set; }
	public string? OsVer { get; set; }
	public string? Lang { get; set; }
	public string? PrefLang { get; set; }
	public List<string>? Screens { get; set; }
	public int? Distr { get; set; }
	public string? ApnsToken { get; set; }
	public DevicePreferences? Preferences { get; set; }
}

/// <summary>
/// 设备偏好设置
/// </summary>
public class DevicePreferences
{
	[JsonPropertyName("enableOnMacbookScreenOnly")]
	public bool? EnableOnMacbookScreenOnly { get; set; }

	[JsonPropertyName("language")]
	public string? Language { get; set; }

	[JsonPropertyName("launchAtLogin")]
	public bool? LaunchAtLogin { get; set; }

	[JsonPropertyName("localImagesEnabled")]
	public bool? LocalImagesEnabled { get; set; }

	[JsonPropertyName("localStorage")]
	public string? LocalStorage { get; set; }

	[JsonPropertyName("makeTheNotchDisappear")]
	public bool? MakeTheNotchDisappear { get; set; }

	[JsonPropertyName("randomWallpaper")]
	public bool? RandomWallpaper { get; set; }

	[JsonPropertyName("randomWallpaperFrequency")]
	public int? RandomWallpaperFrequency { get; set; }

	[JsonPropertyName("randomWallpaperFromMyLibrary")]
	public bool? RandomWallpaperFromMyLibrary { get; set; }

	[JsonPropertyName("setWallpaperForScreens")]
	public bool? SetWallpaperForScreens { get; set; }

	[JsonPropertyName("showIconInDock")]
	public bool? ShowIconInDock { get; set; }

	[JsonPropertyName("showStickyRandomWallpapers")]
	public bool? ShowStickyRandomWallpapers { get; set; }

	[JsonPropertyName("updateAuto")]
	public bool? UpdateAuto { get; set; }
}

/// <summary>
/// UpdateDevice / CheckinDevice 通用 GraphQL 请求体
/// </summary>
public class DeviceGraphQL
{
	public required string Query { get; set; }
	public required string OperationName { get; set; }
	public DeviceVariables Variables { get; set; } = new();
}

/// <summary>
/// UpdateDevice / CheckinDevice 响应
/// </summary>
public class DeviceResponse
{
	public required DeviceResponseData Data { get; set; }
}

public class DeviceResponseData
{
	[JsonPropertyName("updateDevice")]
	public DeviceInfo? UpdateDevice { get; set; }

	[JsonPropertyName("checkinDevice")]
	public DeviceInfo? CheckinDevice { get; set; }
}

public class DeviceInfo
{
	[JsonPropertyName("__typename")]
	public required string Typename { get; set; }

	public required string Id { get; set; }
	public string? Token { get; set; }
	public bool? NvAvl { get; set; }
	public string? Rs { get; set; }
}
