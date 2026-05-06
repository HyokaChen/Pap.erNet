using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Pap.erNet.Models;

namespace Pap.erNet.Utils;

/// <summary>
/// Paper 认证服务
/// 负责设备签到、Token 管理等完整认证流程
/// </summary>
public class AuthService
{
	public static AuthService Instance { get; } = new();

	private const string PAPER_GRAPHQL_URL = "https://paper.nsns.in/graphql";
	private const string PAPER_ENDPOINT_URL = "https://paper.nsns.in/api/endpoint";

	private static readonly HttpClient AuthHttpClient = new(
		new SocketsHttpHandler
		{
			UseProxy = false,
			AllowAutoRedirect = true,
			SslOptions = new() { RemoteCertificateValidationCallback = (_, _, _, _) => true },
		}
	)
	{
		Timeout = TimeSpan.FromSeconds(30),
	};

	static AuthService()
	{
		AuthHttpClient.DefaultRequestHeaders.Host = "paper.nsns.in";
		AuthHttpClient.DefaultRequestHeaders.Connection.Add("keep-alive");
		AuthHttpClient.DefaultRequestHeaders.Add("locale", "zh-Hans");
		AuthHttpClient.DefaultRequestHeaders.Add("client-version", AppConstants.ClientVersion);
		AuthHttpClient.DefaultRequestHeaders.Add("apollographql-client-version", AppConstants.ApolloClientVersion);
		AuthHttpClient.DefaultRequestHeaders.Add("apollographql-client-name", AppConstants.APOLLO_CLIENT_NAME);
		AuthHttpClient.DefaultRequestHeaders.UserAgent.ParseAdd(DeviceUtil.GetGraphQlUserAgent());
	}

	/// <summary>
	/// 当前 Token (JWT)
	/// </summary>
	public string? Token { get; private set; }

	/// <summary>
	/// 设备 ID（服务器返回的）
	/// </summary>
	public string? DeviceId { get; private set; }

	/// <summary>
	/// 设备唯一标识 (did/uid)
	/// </summary>
	public string DeviceUid { get; private set; } = DeviceUtil.GetDeviceUid();

	/// <summary>
	/// 是否已认证
	/// </summary>
	public bool IsAuthenticated => !string.IsNullOrEmpty(Token);

	/// <summary>
	/// 获取用于后续请求的 Authorization Header 值
	/// </summary>
	public string? GetAuthorizationHeader() => string.IsNullOrEmpty(Token) ? null : $"Bearer {Token}";

	/// <summary>
	/// 执行完整认证流程：Endpoint 探测 → CheckinDevice
	/// 严格按照原始 pap.er 应用的启动流程
	/// </summary>
	public async Task<bool> AuthenticateAsync()
	{
		LogHelper.WriteLogAsync($"AuthService: 开始认证流程, UID={DeviceUid}");

		try
		{
			// 步骤 1: 探测端点
			var endpointOk = await CheckEndpointAsync();
			LogHelper.WriteLogAsync($"AuthService: 端点探测结果 = {endpointOk}");

			// 步骤 2: CheckinDevice - 设备签到获取 Token
			var checkinOk = await CheckinDeviceAsync();
			if (!checkinOk)
			{
				LogHelper.WriteLogAsync("AuthService: CheckinDevice 失败");
				return false;
			}

			LogHelper.WriteLogAsync($"AuthService: 认证成功, DeviceId={DeviceId}, Token 长度={Token?.Length ?? 0}");
			return true;
		}
		catch (Exception ex)
		{
			LogHelper.WriteLogAsync($"AuthService: 认证异常 - {ex.Message}\n{ex.StackTrace}");
			return false;
		}
	}

	/// <summary>
	/// 步骤 1: 探测端点（使用 Alamofire 风格 User-Agent）
	/// </summary>
	private async Task<bool> CheckEndpointAsync()
	{
		try
		{
			using var request = new HttpRequestMessage(HttpMethod.Get, PAPER_ENDPOINT_URL);
			request.Headers.UserAgent.ParseAdd(DeviceUtil.GetEndpointUserAgent());
			request.Headers.Host = "paper.nsns.in";
			request.Headers.AcceptLanguage.ParseAdd("zh-Hans-CN;q=1.0");
			request.Headers.Connection.Add("keep-alive");

			using var response = await AuthHttpClient.SendAsync(request).ConfigureAwait(false);
			return response.IsSuccessStatusCode;
		}
		catch (Exception ex)
		{
			LogHelper.WriteLogAsync($"AuthService.CheckEndpoint: {ex.Message}");
			return false;
		}
	}

	/// <summary>
	/// 步骤 2: CheckinDevice - 设备签到获取 Token
	/// </summary>
	private async Task<bool> CheckinDeviceAsync()
	{
		const string query = """
			mutation CheckinDevice($uid: String, $appVer: String, $appBuild: Float, $deviceModel: String, $osName: String, $osVer: String, $lang: String, $prefLang: String, $screens: [String], $distr: Int, $apnsToken: String, $preferences: PreferencesInput) {
			  checkinDevice(
			    uid: $uid
			    appVer: $appVer
			    appBuild: $appBuild
			    deviceModel: $deviceModel
			    osName: $osName
			    osVer: $osVer
			    lang: $lang
			    prefLang: $prefLang
			    screens: $screens
			    distr: $distr
			    apnsToken: $apnsToken
			    preferences: $preferences
			  ) {
			    __typename
			    id
			    token
			    nvAvl
			    rs
			  }
			}
			""";

		var localStoragePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "pap.er");

		var requestBody = new DeviceGraphQL
		{
			Query = query,
			OperationName = "CheckinDevice",
			Variables = new DeviceVariables
			{
				Uid = DeviceUid,
				AppVer = AppConstants.APP_VERSION,
				AppBuild = AppConstants.APP_BUILD,
				DeviceModel = DeviceUtil.GetDeviceModel(),
				OsName = DeviceUtil.GetOsName(),
				OsVer = DeviceUtil.GetOsVersion(),
				Lang = "zh",
				PrefLang = "zh-Hans",
				Screens = DeviceUtil.GetScreens(),
				Distr = DeviceUtil.GetDistribution(),
				ApnsToken = "0084691cadb326d5bebadd0f1530548d3dd8c6b7b4deb76df3e396a7ec6bdd92",
				Preferences = new DevicePreferences
				{
					Language = "zh-Hans",
					LaunchAtLogin = true,
					LocalImagesEnabled = true,
					LocalStorage = localStoragePath,
					MakeTheNotchDisappear = false,
					RandomWallpaper = false,
					RandomWallpaperFrequency = 1,
					RandomWallpaperFromMyLibrary = false,
					SetWallpaperForScreens = false,
					ShowIconInDock = true,
					ShowStickyRandomWallpapers = true,
					UpdateAuto = true,
				},
			},
		};

		var response = await SendDeviceRequestAsync(requestBody, "CheckinDevice", "mutation").ConfigureAwait(false);
		if (response?.Data?.CheckinDevice == null)
		{
			LogHelper.WriteLogAsync("AuthService.CheckinDevice: 响应为空");
			return false;
		}

		var checkinData = response.Data.CheckinDevice;
		DeviceId = checkinData.Id;
		Token = checkinData.Token;

		if (string.IsNullOrEmpty(Token))
		{
			LogHelper.WriteLogAsync("AuthService.CheckinDevice: Token 为空，签到可能失败");
			return false;
		}

		LogHelper.WriteLogAsync($"AuthService.CheckinDevice: 成功, DeviceId={DeviceId}, NvAvl={checkinData.NvAvl}");
		return true;
	}

	/// <summary>
	/// 更新设备偏好设置（已认证后调用）
	/// </summary>
	public async Task<bool> UpdatePreferencesAsync(DevicePreferences preferences)
	{
		const string query = """
			mutation UpdateDevice($uid: String, $appVer: String, $appBuild: Float, $deviceModel: String, $osName: String, $osVer: String, $lang: String, $prefLang: String, $screens: [String], $distr: Int, $apnsToken: String, $preferences: PreferencesInput) {
			  updateDevice(
			    uid: $uid
			    appVer: $appVer
			    appBuild: $appBuild
			    deviceModel: $deviceModel
			    osName: $osName
			    osVer: $osVer
			    lang: $lang
			    prefLang: $prefLang
			    screens: $screens
			    distr: $distr
			    apnsToken: $apnsToken
			    preferences: $preferences
			  ) {
			    __typename
			    id
			    token
			    nvAvl
			    rs
			  }
			}
			""";

		var requestBody = new DeviceGraphQL
		{
			Query = query,
			OperationName = "UpdateDevice",
			Variables = new DeviceVariables { Uid = DeviceUid, Preferences = preferences },
		};

		var response = await SendDeviceRequestAsync(requestBody, "UpdateDevice", "mutation").ConfigureAwait(false);
		return response?.Data?.UpdateDevice != null;
	}

	/// <summary>
	/// 发送设备相关的 GraphQL 请求
	/// </summary>
	private async Task<DeviceResponse?> SendDeviceRequestAsync(DeviceGraphQL requestBody, string operationName, string operationType)
	{
		try
		{
			var jsonString = JsonSerializer.Serialize(requestBody, AuthSourceGenerationContext.Default.DeviceGraphQL);
			var requestMessage = new HttpRequestMessage(HttpMethod.Post, PAPER_GRAPHQL_URL)
			{
				Content = new StringContent(jsonString, Encoding.UTF8, new MediaTypeHeaderValue("application/json")),
			};

			requestMessage.Headers.Add("did", DeviceUid);
			requestMessage.Headers.Add("X-APOLLO-OPERATION-NAME", operationName);
			requestMessage.Headers.Add("X-APOLLO-OPERATION-TYPE", operationType);

			var authHeader = GetAuthorizationHeader();
			if (authHeader != null)
			{
				requestMessage.Headers.Add("Authorization", authHeader);
			}

			using var httpResponse = await AuthHttpClient.SendAsync(requestMessage).ConfigureAwait(false);
			var responseStr = await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);

			LogHelper.WriteLogAsync($"AuthService.SendDeviceRequest: {operationName} HTTP {(int)httpResponse.StatusCode}");

			if (!httpResponse.IsSuccessStatusCode)
			{
				LogHelper.WriteLogAsync(
					$"AuthService.SendDeviceRequest: 请求失败, StatusCode={httpResponse.StatusCode}, Body={responseStr}"
				);
				return null;
			}

			return JsonSerializer.Deserialize(responseStr, AuthSourceGenerationContext.Default.DeviceResponse);
		}
		catch (Exception ex)
		{
			LogHelper.WriteLogAsync($"AuthService.SendDeviceRequest: {operationName} 异常 - {ex.Message}\n{ex.StackTrace}");
			return null;
		}
	}
}

/// <summary>
/// 认证相关模型的 JSON 序列化上下文（Source Generator）
/// </summary>
[System.Text.Json.Serialization.JsonSourceGenerationOptions(
	WriteIndented = true,
	PropertyNamingPolicy = System.Text.Json.Serialization.JsonKnownNamingPolicy.CamelCase
)]
[System.Text.Json.Serialization.JsonSerializable(typeof(DeviceGraphQL))]
[System.Text.Json.Serialization.JsonSerializable(typeof(DeviceVariables))]
[System.Text.Json.Serialization.JsonSerializable(typeof(DevicePreferences))]
[System.Text.Json.Serialization.JsonSerializable(typeof(DeviceResponse))]
[System.Text.Json.Serialization.JsonSerializable(typeof(DeviceResponseData))]
[System.Text.Json.Serialization.JsonSerializable(typeof(DeviceInfo))]
public partial class AuthSourceGenerationContext : System.Text.Json.Serialization.JsonSerializerContext { }
