using System.Net.Security;

namespace Pap.erNet.Utils;

/// <summary>
/// CDN 域名解析器：从官方状态接口获取实际 CDN 域名，替换 API 返回的 cdn.paperapp.net
/// </summary>
public static class CdnResolver
{
	private const string CdnStatusUrl = "https://www.paperapp.net/status/cdn.txt";
	private static string? _resolvedCdnHost;
	private static readonly SemaphoreSlim ResolveLock = new(1, 1);

	/// <summary>
	/// 获取解析后的 CDN 域名（带缓存）
	/// </summary>
	public static async Task<string> GetResolvedCdnHostAsync()
	{
		if (!string.IsNullOrEmpty(_resolvedCdnHost))
		{
			return _resolvedCdnHost;
		}

		await ResolveLock.WaitAsync();
		try
		{
			if (!string.IsNullOrEmpty(_resolvedCdnHost))
			{
				return _resolvedCdnHost;
			}

			using var client = new HttpClient(
				new SocketsHttpHandler
				{
					UseProxy = false,
					SslOptions = new SslClientAuthenticationOptions
					{
						RemoteCertificateValidationCallback = (_, _, _, _) => true,
						EnabledSslProtocols =
							System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13,
					},
				}
			);
			client.Timeout = TimeSpan.FromSeconds(10);

			client.DefaultRequestHeaders.UserAgent.ParseAdd(DeviceUtil.GetImageDownloadUserAgent());

			LogHelper.WriteLogAsync($"[CDN] 正在解析 CDN 域名: {CdnStatusUrl}");
			var response = await client.GetStringAsync(CdnStatusUrl).ConfigureAwait(false);
			var host = response.Trim();

			if (string.IsNullOrEmpty(host))
			{
				LogHelper.WriteLogAsync("[CDN] 解析结果为空，使用默认域名 cdn.paperapp.net");
				_resolvedCdnHost = "cdn.paperapp.net";
				return _resolvedCdnHost;
			}

			LogHelper.WriteLogAsync($"[CDN] 解析成功: cdn.paperapp.net -> {host}");
			_resolvedCdnHost = host;
			return _resolvedCdnHost;
		}
		catch (Exception ex)
		{
			LogHelper.WriteLogAsync($"[CDN] 解析失败: {ex.Message}，使用默认域名 cdn.paperapp.net");
			_resolvedCdnHost = "cdn.paperapp.net";
			return _resolvedCdnHost;
		}
		finally
		{
			ResolveLock.Release();
		}
	}

	/// <summary>
	/// 替换 URL 中的 cdn.paperapp.net 为解析后的 CDN 域名
	/// </summary>
	public static string ResolveCdnUrl(string url)
	{
		if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(_resolvedCdnHost) || _resolvedCdnHost == "cdn.paperapp.net")
			return url;

		return url.Replace("cdn.paperapp.net", _resolvedCdnHost, StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>
	/// 预解析 CDN 域名（在应用启动时调用）
	/// </summary>
	public static void PreResolve()
	{
		_ = Task.Run(async () => await GetResolvedCdnHostAsync());
	}
}
