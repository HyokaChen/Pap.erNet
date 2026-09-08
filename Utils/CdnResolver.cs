using System.Net.Security;

namespace Pap.erNet.Utils;

/// <summary>
/// CDN 域名解析器：候选池 + 真实探测，选出当前网络环境下真正可用的 CDN 域名。
/// 背景：API 返回的图片 URL 使用 r2.paperapp.net (Cloudflare)，部分网络对该域 SNI 阻断，
/// TLS 握手包被静默丢弃，连接池每次尝试恰好卡 SocketsHttpHandler 默认 ConnectTimeout=10s。
/// 状态接口 (status/cdn.txt) 给出的域名也可能与被阻断域名同属 Cloudflare，故必须逐个探测。
/// </summary>
public static class CdnResolver
{
	private const string CdnStatusUrl = "https://www.paperapp.net/status/cdn.txt";

	// 探测超时：SNI 阻断表现为静默丢包（无 RST），3 秒足够区分"通/不通"，
	// 远小于默认 ConnectTimeout=10s，避免候选池串行探测拖慢启动
	private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(3);

	// 探测用固定缩略图路径：任何 CDN 主机上都存在（已验证 r2/c2/cdn 字节级等价）
	private const string ProbePath = "/photos/473ffa3169cdb2b47f0981f4f2063d3e.jpg/thumb";

	// 同一主机连续下载失败达到该次数后触发重新解析（换 CDN 域名）
	private const int FailureThreshold = 6;

	// 失效后到允许再次重解析的最短间隔：防止多张图片同时失败引发的解析风暴
	private static readonly TimeSpan ReResolveMinInterval = TimeSpan.FromSeconds(30);

	private static string? _resolvedCdnHost;
	private static readonly SemaphoreSlim ResolveLock = new(1, 1);

	private static int _consecutiveFailures;
	private static DateTime _lastInvalidatedUtc = DateTime.MinValue;

	// 已知 CDN 主机：cdn.* 是官方文档域名；r2.* 是 API 实际返回的 R2 存储域名。
	// 两者都必须重写到健康 CDN，否则 r2 被阻断时无兜底
	private static readonly string[] KnownCdnHosts = ["cdn.paperapp.net", "r2.paperapp.net"];

	// 候选池：状态接口域名排最前（官方推荐），其余按历史可用性排序。
	// c11/c12 是 2026-07 状态接口返回的 Vercel 线路，c2 是 2026-09 当前值
	private static readonly string[] FallbackCdnHosts =
	[
		"c2.paperapp.net",
		"cdn.paperapp.net",
		"r2.paperapp.net",
		"c11.paperapp.net",
		"c12.paperapp.net",
	];

	/// <summary>
	/// 获取当前网络环境下真实可用的 CDN 域名（带缓存）。
	/// 候选顺序：状态接口域名 → 固定候选池；逐个探测，选第一个 TLS+HTTP 全链路通的。
	/// </summary>
	public static async Task<string> GetResolvedCdnHostAsync()
	{
		if (!string.IsNullOrEmpty(_resolvedCdnHost))
		{
			return _resolvedCdnHost;
		}

		await ResolveLock.WaitAsync().ConfigureAwait(false);
		try
		{
			if (!string.IsNullOrEmpty(_resolvedCdnHost))
			{
				return _resolvedCdnHost;
			}

			var candidates = await BuildCandidatesAsync().ConfigureAwait(false);
			foreach (var host in candidates)
			{
				if (await ProbeHostAsync(host).ConfigureAwait(false))
				{
					_resolvedCdnHost = host;
					LogHelper.WriteLogAsync($"[CDN] 探测通过: {host}（候选顺序: {string.Join(" > ", candidates)}）");
					return _resolvedCdnHost;
				}

				LogHelper.WriteLogAsync($"[CDN] 探测失败，跳过: {host}");
			}

			// 全部失败：保留原始域名不改写（与应用外行为一致），后续失败会触发重解析
			_resolvedCdnHost = KnownCdnHosts[0];
			LogHelper.WriteLogAsync($"[CDN] 所有候选均不可达，暂用 {KnownCdnHosts[0]} 并等待重解析");
			return _resolvedCdnHost;
		}
		finally
		{
			ResolveLock.Release();
		}
	}

	/// <summary>
	/// 构建 CDN 候选序列：状态接口域名（若可获取）优先，其余固定候选池。
	/// 状态接口本身不可达时仅用固定候选池（它也在探测名单里，不影响可用性）。
	/// </summary>
	private static async Task<string[]> BuildCandidatesAsync()
	{
		var statusHost = await TryFetchStatusHostAsync().ConfigureAwait(false);
		var candidates = new List<string>(FallbackCdnHosts.Length + 1);

		if (!string.IsNullOrEmpty(statusHost))
		{
			candidates.Add(statusHost);
		}

		foreach (var host in FallbackCdnHosts)
		{
			if (!candidates.Contains(host, StringComparer.OrdinalIgnoreCase))
			{
				candidates.Add(host);
			}
		}

		return [.. candidates];
	}

	/// <summary>
	/// 从状态接口获取官方推荐的 CDN 域名（10 秒内）。失败返回 null（不抛出）。
	/// </summary>
	private static async Task<string?> TryFetchStatusHostAsync()
	{
		try
		{
			using var client = CreateProbeClient(TimeSpan.FromSeconds(10));
			LogHelper.WriteLogAsync($"[CDN] 正在解析 CDN 域名: {CdnStatusUrl}");
			var response = await client.GetStringAsync(CdnStatusUrl).ConfigureAwait(false);
			var host = response.Trim();

			if (string.IsNullOrEmpty(host))
			{
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
	/// 真实探测候选主机：拉取固定缩略图路径，TLS 握手 + HTTP 200 + 非空响应体全通过才算健康。
	/// 这比 DNS/ping 检测更可靠——SNI 阻断下 DNS 正常、TCP 可能通，只有带 SNI 的 TLS 才暴露。
	/// </summary>
	private static async Task<bool> ProbeHostAsync(string host)
	{
		try
		{
			using var client = CreateProbeClient(ProbeTimeout);
			var bytes = await client.GetByteArrayAsync($"https://{host}{ProbePath}").ConfigureAwait(false);
			return bytes.Length > 0;
		}
		catch (Exception ex)
		{
			LogHelper.WriteLogAsync($"[CDN] 探测 {host} 异常: {ex.GetType().Name}: {ex.Message}");
			return false;
		}
	}

	/// <summary>
	/// 创建探测专用 HttpClient。UseProxy=true：探测的目的是评估"用户网络下当前哪个域名可用"，
	/// 用户的系统代理属于该网络环境的一部分；无代理环境 .NET 自动回退直连。
	/// </summary>
	private static HttpClient CreateProbeClient(TimeSpan timeout)
	{
		var client = new HttpClient(
			new SocketsHttpHandler
			{
				UseProxy = true,
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
	/// 替换 URL 中的已知 CDN 域名（cdn.paperapp.net / r2.paperapp.net）为当前健康 CDN。
	/// API 返回的缩略图/原图 URL 使用 r2.paperapp.net（Cloudflare，部分网络 SNI 阻断导致
	/// TLS 握手静默丢包、默认 ConnectTimeout=10s 反复超时），必须整体重写到健康域名。
	/// </summary>
	public static string ResolveCdnUrl(string url)
	{
		if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(_resolvedCdnHost) || _resolvedCdnHost == KnownCdnHosts[0])
			return url;

		foreach (var host in KnownCdnHosts)
		{
			if (url.Contains(host, StringComparison.OrdinalIgnoreCase))
			{
				return url.Replace(host, _resolvedCdnHost, StringComparison.OrdinalIgnoreCase);
			}
		}

		return url;
	}

	/// <summary>
	/// 下载层持续失败时（连续 {FailureThreshold} 次连接类失败）调用：失效当前 CDN 选择，
	/// 由下一次 ResolveCdnUrl/GetResolvedCdnHostAsync 重新探测。带最短间隔限流。
	/// </summary>
	public static void ReportDownloadFailure()
	{
		var count = Interlocked.Increment(ref _consecutiveFailures);
		if (count < FailureThreshold)
			return;

		// 限流：到点才失效，未到点直接返回（保持现选择，等下一轮）
		var now = DateTime.UtcNow;
		var last = _lastInvalidatedUtc;
		if (now - last < ReResolveMinInterval || _lastInvalidatedUtc != last)
			return;

		if (Interlocked.CompareExchange(ref _lastInvalidatedUtc, now, last) == last)
		{
			Interlocked.Exchange(ref _consecutiveFailures, 0);
			var old = _resolvedCdnHost;
			_resolvedCdnHost = null;
			LogHelper.WriteLogAsync(
				$"[CDN] 连续 {count} 次下载失败，失效 {old ?? "(null)"}，待重新探测（间隔限流 {ReResolveMinInterval.TotalSeconds}s）"
			);
			// 后台立即触发重探测，不等下一张图
			_ = Task.Run(async () => await GetResolvedCdnHostAsync());
		}
	}

	/// <summary>
	/// 下载成功一次即清零失败计数
	/// </summary>
	public static void ReportDownloadSuccess()
	{
		Interlocked.Exchange(ref _consecutiveFailures, 0);
	}

	/// <summary>
	/// 预解析 CDN 域名（在应用启动时调用）
	/// </summary>
	public static void PreResolve()
	{
		_ = Task.Run(async () => await GetResolvedCdnHostAsync());
	}
}
