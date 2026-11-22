using System.Collections.Concurrent;
using System.Net.Security;

namespace Pap.erNet.Utils;

/// <summary>
/// CDN 域名解析器：候选池 + 真实探测，选出当前网络环境下真正可用的 CDN 域名。
/// 背景：API 返回的图片域名会变化（c3.wuse.co → r2/cdn.paperapp.net → img.paperapp.net），
/// 且不同网络对 Cloudflare 域名的可达性不同。下载前统一把“任意图片镜像域名”重写到
/// 当前探测通过的域名，避免 API 换域名后旧白名单失效导致整批图片加载失败。
/// </summary>
public static class CdnResolver
{
	private const string CdnStatusUrl = "https://www.paperapp.net/status/cdn.txt";

	// 探测超时：SNI 阻断表现为静默丢包（无 RST），2.5 秒足够区分“通/不通”，
	// 远小于 SocketsHttpHandler 默认 ConnectTimeout=10s；所有候选并发探测，不串行累加。
	private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(2.5);

	// 全部候选失败后的冷却时间：期间保留 API 原始 URL，不反复探测、不刷日志。
	private static readonly TimeSpan ResolveFailureCooldown = TimeSpan.FromSeconds(15);

	// 单个域名探测失败/下载失败后的冷却时间：下一轮优先换别的域名，避免反复命中坏域名。
	private static readonly TimeSpan HostFailureCooldown = TimeSpan.FromMinutes(2);

	// 同一域名连续下载失败达到该次数后触发重新解析（换 CDN 域名）。
	// 每次图片加载最多尝试 4 次，阈值 3 可让第一张图在最后一次重试时就用上兜底域名。
	private const int FailureThreshold = 3;

	// 失效后到允许再次重解析的最短间隔：防止多张图片同时失败引发的解析风暴。
	private static readonly TimeSpan ReResolveMinInterval = TimeSpan.FromSeconds(10);

	// 探测用固定缩略图路径：所有镜像主机返回同一份 R2 数据（已实测 img/c11/r2 字节级等价）。
	private const string ProbePath = "/photos/473ffa3169cdb2b47f0981f4f2063d3e.jpg/thumb";

	private static string? _resolvedCdnHost;
	private static DateTime _nextResolveAttemptUtc = DateTime.MinValue;
	private static readonly SemaphoreSlim ResolveLock = new(1, 1);

	private static int _consecutiveFailures;
	private static DateTime _lastInvalidatedUtc = DateTime.MinValue;
	private static readonly object InvalidateLock = new();

	// 当前 API 实际返回的新图片域名优先；状态接口若返回更优域名会排在它前面。
	// r2/cdn 已从主动探测池移除，仅保留在“识别并重写”规则中兼容旧响应与本地缓存。
	private static readonly string[] FallbackCdnHosts =
	[
		"img.paperapp.net",
		"c11.paperapp.net",
		"c2.paperapp.net",
		"c12.paperapp.net",
		"cdn.paperapp.net",
	];

	// 域名级冷却：探测失败或实际下载失败后，短时间内不再优先选择它。
	private static readonly ConcurrentDictionary<string, DateTime> HostCooldownUntil = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// 获取当前网络环境下真实可用的 CDN 域名（带缓存）。
	/// 候选顺序：状态接口域名 → 固定候选池；并发探测，按优先级选第一个 TLS+HTTP 全链路通的。
	/// 全部不可达时返回 null，调用方保留 API 原始 URL。
	/// </summary>
	public static async Task<string?> GetResolvedCdnHostAsync()
	{
		if (HasHealthyResolvedHost())
		{
			return _resolvedCdnHost;
		}

		if (DateTime.UtcNow < _nextResolveAttemptUtc)
		{
			return null;
		}

		await ResolveLock.WaitAsync().ConfigureAwait(false);
		try
		{
			if (HasHealthyResolvedHost())
			{
				return _resolvedCdnHost;
			}

			if (DateTime.UtcNow < _nextResolveAttemptUtc)
			{
				return null;
			}

			var candidates = await BuildCandidatesAsync().ConfigureAwait(false);
			var available = candidates.Where(host => !IsHostCoolingDown(host)).ToArray();
			if (available.Length == 0)
			{
				_resolvedCdnHost = null;
				_nextResolveAttemptUtc = DateTime.UtcNow + ResolveFailureCooldown;
				LogHelper.WriteLogAsync("[CDN] 所有候选均在失败冷却期，暂保留原始图片域名");
				return null;
			}

			// 并发探测、按候选优先级取第一个成功者：
			// 坏域名不会串行拖满 2.5s × N，状态接口域名仍拥有最高优先级。
			var probes = available.Select(ProbeHostAsync).ToArray();
			for (var i = 0; i < available.Length; i++)
			{
				var host = available[i];
				if (await probes[i].ConfigureAwait(false))
				{
					_resolvedCdnHost = host;
					_nextResolveAttemptUtc = DateTime.MinValue;
					HostCooldownUntil.TryRemove(host, out _);
					LogHelper.WriteLogAsync($"[CDN] 探测通过: {host}（候选: {string.Join(" > ", available)}）");
					return host;
				}

				MarkHostCoolingDown(host);
				LogHelper.WriteLogAsync($"[CDN] 探测失败，跳过: {host}");
			}

			_resolvedCdnHost = null;
			_nextResolveAttemptUtc = DateTime.UtcNow + ResolveFailureCooldown;
			LogHelper.WriteLogAsync("[CDN] 所有候选均不可达，保留 API 原始图片域名");
			return null;
		}
		finally
		{
			ResolveLock.Release();
		}
	}

	private static bool HasHealthyResolvedHost() => !string.IsNullOrEmpty(_resolvedCdnHost) && !IsHostCoolingDown(_resolvedCdnHost);

	/// <summary>
	/// 构建 CDN 候选序列：状态接口域名（若可获取且合法）优先，其余固定候选池。
	/// </summary>
	private static async Task<string[]> BuildCandidatesAsync()
	{
		var statusHost = await TryFetchStatusHostAsync().ConfigureAwait(false);
		var candidates = new List<string>(FallbackCdnHosts.Length + 1);

		AddCandidate(candidates, statusHost);
		foreach (var host in FallbackCdnHosts)
		{
			AddCandidate(candidates, host);
		}

		return [.. candidates];
	}

	private static void AddCandidate(List<string> candidates, string? host)
	{
		if (!TryNormalizeImageHost(host, out var normalized))
		{
			return;
		}

		if (!candidates.Contains(normalized, StringComparer.OrdinalIgnoreCase))
		{
			candidates.Add(normalized);
		}
	}

	/// <summary>
	/// 从状态接口获取官方推荐的 CDN 域名（5 秒内）。失败返回 null（不抛出）。
	/// 状态文件可能包含多行或带协议，只取第一个合法图片域名。
	/// </summary>
	private static async Task<string?> TryFetchStatusHostAsync()
	{
		try
		{
			using var client = CreateProbeClient(TimeSpan.FromSeconds(3));
			LogHelper.WriteLogAsync($"[CDN] 正在解析 CDN 域名: {CdnStatusUrl}");
			var response = await client.GetStringAsync(CdnStatusUrl).ConfigureAwait(false);
			var host = ParseStatusHost(response);
			if (host is null)
			{
				LogHelper.WriteLogAsync("[CDN] 状态接口未返回合法图片域名");
				return null;
			}

			LogHelper.WriteLogAsync($"[CDN] 状态接口返回: {host}");
			return host;
		}
		catch (Exception ex)
		{
			LogHelper.WriteLogAsync($"[CDN] 状态接口不可达: {ex.Message}");
			return null;
		}
	}

	/// <summary>
	/// 从 CDN 状态文件文本中解析第一个合法图片镜像域名。
	/// 支持多行、注释行、带协议/路径的写法；纯函数，便于单元测试。
	/// </summary>
	internal static string? ParseStatusHost(string response)
	{
		if (string.IsNullOrWhiteSpace(response))
		{
			return null;
		}

		foreach (var token in response.Split(['\r', '\n', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries))
		{
			if (TryNormalizeImageHost(token, out var candidate))
			{
				return candidate;
			}
		}

		return null;
	}

	/// <summary>
	/// 真实探测候选主机：拉取固定缩略图路径，TLS 握手 + HTTP 200 + 图片响应头全通过才算健康。
	/// 只读响应头、不下载图片体，避免启动阶段为探测消耗带宽。
	/// </summary>
	private static async Task<bool> ProbeHostAsync(string host)
	{
		try
		{
			using var client = CreateProbeClient(ProbeTimeout);
			using var request = new HttpRequestMessage(HttpMethod.Get, $"https://{host}{ProbePath}");
			request.Headers.Accept.ParseAdd("image/avif,image/webp,image/*,*/*;q=0.9");

			using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);

			if (!response.IsSuccessStatusCode)
			{
				LogHelper.WriteLogAsync($"[CDN] 探测 {host} 返回 HTTP {(int)response.StatusCode}");
				return false;
			}

			var mediaType = response.Content.Headers.ContentType?.MediaType;
			var contentLength = response.Content.Headers.ContentLength;
			var isImage =
				mediaType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true
				|| string.Equals(mediaType, "application/octet-stream", StringComparison.OrdinalIgnoreCase)
				|| (mediaType is null && contentLength is > 0);
			if (isImage)
			{
				return true;
			}

			LogHelper.WriteLogAsync($"[CDN] 探测 {host} 响应为空或非图片: {mediaType ?? "(null)"}");
			return false;
		}
		catch (Exception ex)
		{
			LogHelper.WriteLogAsync($"[CDN] 探测 {host} 异常: {ex.GetType().Name}: {ex.Message}");
			return false;
		}
	}

	/// <summary>
	/// 创建探测专用 HttpClient。UseProxy=true：探测的目的是评估“用户网络下当前哪个域名可用”，
	/// 用户的系统代理属于该网络环境的一部分；无代理环境 .NET 自动回退直连。
	/// </summary>
	private static HttpClient CreateProbeClient(TimeSpan timeout)
	{
		var client = new HttpClient(
			new SocketsHttpHandler
			{
				UseProxy = true,
				ConnectTimeout = TimeSpan.FromSeconds(3),
				SslOptions = new SslClientAuthenticationOptions
				{
					RemoteCertificateValidationCallback = (_, _, _, _) => true,
					EnabledSslProtocols =
						System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13,
				},
			}
		)
		{
			Timeout = timeout,
		};
		client.DefaultRequestHeaders.UserAgent.ParseAdd(DeviceUtil.GetImageDownloadUserAgent());
		return client;
	}

	/// <summary>
	/// 把任意已知图片镜像域名（img/cdn/r2/c*.paperapp.net、旧 c*.wuse.co）重写为当前健康域名。
	/// 只改 scheme/host，保留 path/query/fragment；非图片 URL 原样返回。
	/// </summary>
	public static string ResolveCdnUrl(string url) => ResolveCdnUrl(url, _resolvedCdnHost);

	internal static string ResolveCdnUrl(string url, string? resolvedCdnHost)
	{
		if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
		{
			return url;
		}

		if (!IsImageCdnHost(uri.Host))
		{
			return url;
		}

		var targetHost = resolvedCdnHost;
		if (string.IsNullOrEmpty(targetHost) || IsHostCoolingDown(targetHost))
		{
			return UpgradeToHttps(uri, url);
		}

		if (uri.Host.Equals(targetHost, StringComparison.OrdinalIgnoreCase) && uri.Scheme == Uri.UriSchemeHttps)
		{
			return url;
		}

		var builder = new UriBuilder(uri) { Scheme = Uri.UriSchemeHttps, Host = targetHost };
		if (uri.IsDefaultPort)
		{
			builder.Port = -1;
		}

		return builder.Uri.ToString();
	}

	private static string UpgradeToHttps(Uri uri, string originalUrl)
	{
		if (uri.Scheme == Uri.UriSchemeHttps)
		{
			return originalUrl;
		}

		var builder = new UriBuilder(uri) { Scheme = Uri.UriSchemeHttps };
		if (uri.IsDefaultPort)
		{
			builder.Port = -1;
		}

		return builder.Uri.ToString();
	}

	/// <summary>
	/// 缓存键：同一张图在不同 CDN 域名下 path 完全一致，用 path+query 作为键，
	/// 域名切换后仍能命中磁盘/内存缓存，不必整批重新下载。
	/// </summary>
	public static string GetStableCacheKey(string url)
	{
		if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
		{
			return url;
		}

		return IsImageCdnHost(uri.Host) ? uri.PathAndQuery : url;
	}

	/// <summary>
	/// 是否为图片镜像域名。用后缀匹配，新增 c13/img2 等域名无需改代码；
	/// www.paperapp.net 是状态页，不参与重写。
	/// </summary>
	public static bool IsImageCdnHost(string? host)
	{
		if (string.IsNullOrWhiteSpace(host))
		{
			return false;
		}

		host = host.Trim().TrimEnd('.');
		if (host.Equals("www.paperapp.net", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		return host.EndsWith(".paperapp.net", StringComparison.OrdinalIgnoreCase)
			|| host.EndsWith(".wuse.co", StringComparison.OrdinalIgnoreCase);
	}

	private static bool TryNormalizeImageHost(string? value, out string host)
	{
		host = string.Empty;
		if (string.IsNullOrWhiteSpace(value))
		{
			return false;
		}

		var candidate = value.Trim();
		if (Uri.TryCreate(candidate, UriKind.Absolute, out var uri))
		{
			candidate = uri.Host;
		}

		candidate = candidate.Trim().TrimEnd('.');
		if (!IsImageCdnHost(candidate))
		{
			return false;
		}

		host = candidate;
		return true;
	}

	/// <summary>
	/// 下载层持续失败时（同一 URL 连续 {FailureThreshold} 次连接类失败）调用：
	/// 把当前域名加入冷却、失效当前选择，由下一次 ResolveCdnUrl/GetResolvedCdnHostAsync 换域名。
	/// 带最短间隔限流，避免多张图并发失败时反复探测。
	/// </summary>
	public static void ReportDownloadFailure()
	{
		var count = Interlocked.Increment(ref _consecutiveFailures);
		if (count < FailureThreshold)
		{
			return;
		}

		var now = DateTime.UtcNow;
		lock (InvalidateLock)
		{
			if (now - _lastInvalidatedUtc < ReResolveMinInterval)
			{
				return;
			}

			_lastInvalidatedUtc = now;
		}

		Interlocked.Exchange(ref _consecutiveFailures, 0);
		var old = _resolvedCdnHost;
		if (!string.IsNullOrEmpty(old))
		{
			MarkHostCoolingDown(old);
		}

		_resolvedCdnHost = null;
		_nextResolveAttemptUtc = DateTime.MinValue;
		LogHelper.WriteLogAsync(
			$"[CDN] 连续 {count} 次下载失败，失效 {old ?? "(null)"}，换域名重试（间隔限流 {ReResolveMinInterval.TotalSeconds}s）"
		);
		_ = Task.Run(async () => await GetResolvedCdnHostAsync());
	}

	/// <summary>
	/// 下载成功一次即清零失败计数，并解除当前域名的失败冷却。
	/// </summary>
	public static void ReportDownloadSuccess()
	{
		Interlocked.Exchange(ref _consecutiveFailures, 0);
		var host = _resolvedCdnHost;
		if (!string.IsNullOrEmpty(host))
		{
			HostCooldownUntil.TryRemove(host, out _);
		}
	}

	private static bool IsHostCoolingDown(string host)
	{
		if (!HostCooldownUntil.TryGetValue(host, out var until))
		{
			return false;
		}

		if (until > DateTime.UtcNow)
		{
			return true;
		}

		HostCooldownUntil.TryRemove(host, out _);
		return false;
	}

	private static void MarkHostCoolingDown(string host) => HostCooldownUntil[host] = DateTime.UtcNow + HostFailureCooldown;

	/// <summary>
	/// 预解析 CDN 域名（在应用启动时调用）
	/// </summary>
	public static void PreResolve()
	{
		_ = Task.Run(async () => await GetResolvedCdnHostAsync());
	}
}
