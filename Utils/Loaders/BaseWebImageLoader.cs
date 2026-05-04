using System.Net;
using System.Net.Http.Headers;
using System.Net.Security;
using Avalonia;
using Avalonia.Logging;
using Avalonia.Media.Imaging;

namespace Pap.erNet.Utils.Loaders;

/// <summary>
///     Provides non cached way to asynchronously load images for <see cref="ImageLoader" />
///     Can be used as base class if you want to create custom caching mechanism
/// </summary>
public class BaseWebImageLoader : IAsyncImageLoader
{
	private readonly ParametrizedLogger? _logger;
	private readonly IHttpClientFactory? _httpClientFactory;

	public BaseWebImageLoader()
	{
		_logger = Logger.TryGet(LogEventLevel.Information, "AsyncImageLoader");
	}

	public BaseWebImageLoader(IHttpClientFactory httpClientFactory)
	{
		_httpClientFactory = httpClientFactory;
		_logger = Logger.TryGet(LogEventLevel.Information, "AsyncImageLoader");
	}

	/// <summary>
	///     获取 HttpClient，优先从 Factory 获取，否则使用静态回退
	/// </summary>
	private HttpClient GetClient()
	{
		if (_httpClientFactory != null)
		{
			return _httpClientFactory.CreateClient(ImageHttpClientNames.ThumbImage);
		}

		// 回退：直接从 App 的 DI 容器获取
		var app = Application.Current as App;
		if (app?.ServicesProvider.GetService(typeof(IHttpClientFactory)) is IHttpClientFactory factory)
		{
			return factory.CreateClient(ImageHttpClientNames.ThumbImage);
		}

		// 最终回退：静态 HttpClient
		return FallbackClient;
	}

	// 回退用的静态 HttpClient（仅当 DI 不可用时使用）
	private static readonly HttpClient FallbackClient = new(
		new SocketsHttpHandler
		{
			UseProxy = false,
			MaxConnectionsPerServer = 2,
			AllowAutoRedirect = true,
			SslOptions = new SslClientAuthenticationOptions { RemoteCertificateValidationCallback = (_, _, _, _) => true },
			AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
			PooledConnectionLifetime = TimeSpan.FromMinutes(5),
		}
	)
	{
		Timeout = TimeSpan.FromSeconds(60),
	};

	static BaseWebImageLoader()
	{
		FallbackClient.DefaultRequestHeaders.UserAgent.ParseAdd("pap.er/39 CFNetwork/3860.200.71 Darwin/25.1.0");
		FallbackClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("image/avif"));
		FallbackClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("image/webp"));
		FallbackClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*") { Quality = 0.8 });
		FallbackClient.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
		FallbackClient.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("deflate"));
		FallbackClient.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("br"));
	}

	/// <inheritdoc />
	public virtual async Task<Bitmap?> ProvideImageAsync(string url)
	{
		// Null check to prevent issues with null URLs
		if (string.IsNullOrEmpty(url))
			return null;

		return await LoadAsync(url).ConfigureAwait(false);
	}

	public void Dispose()
	{
		GC.SuppressFinalize(this);
	}

	/// <summary>
	///     Attempts to load bitmap
	/// </summary>
	/// <param name="url">Target url</param>
	/// <returns>Bitmap</returns>
	protected virtual async Task<Bitmap?> LoadAsync(string url)
	{
		var internalOrCachedBitmap = await LoadFromGlobalCache(url).ConfigureAwait(false);
		if (internalOrCachedBitmap != null)
			return internalOrCachedBitmap;

		try
		{
			var externalBytes = await LoadDataFromExternalAsync(url).ConfigureAwait(false);
			if (externalBytes == null)
				return null;

			using var memoryStream = new MemoryStream(externalBytes);
			var bitmap = new Bitmap(memoryStream);
			await SaveToGlobalCache(url, externalBytes).ConfigureAwait(false);
			return bitmap;
		}
		catch (Exception ex)
		{
			LogHelper.WriteLogAsync($"[图片加载] 异常: {ex.Message}");
			return null;
		}
	}

	/// <summary>
	///     Receives image bytes from an external source (for example, from the Internet).
	///     This data will be cached globally (if required by the current implementation)
	/// </summary>
	/// <param name="url">Target url</param>
	/// <returns>Image bytes</returns>
	protected virtual async Task<byte[]?> LoadDataFromExternalAsync(string url)
	{
		LogHelper.WriteLogAsync($"[Network] 开始下载: {url}");

		var client = GetClient();

		const int maxRetries = 3;
		const int initialDelay = 1000;

		for (var attempt = 0; attempt <= maxRetries; attempt++)
		{
			if (attempt > 0)
			{
				var delay = (int)(initialDelay * Math.Pow(2, attempt - 1));
				LogHelper.WriteLogAsync($"[Network] 第 {attempt} 次重试，等待 {delay}ms: {url}");
				await Task.Delay(delay);
			}

			try
			{
				// 阶段1：发送请求并获取响应头（15秒超时）
				using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
				using var request = new HttpRequestMessage(HttpMethod.Get, url);
				request.Headers.Host = "c3.wuse.co";

				LogHelper.WriteLogAsync($"[Network] 发送请求: {url}");
				using var response = await client
					.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token)
					.ConfigureAwait(false);
				LogHelper.WriteLogAsync($"[Network] 收到响应: {url}, Status={response.StatusCode}");

				if (!response.IsSuccessStatusCode)
				{
					LogHelper.WriteLogAsync($"[Network] HTTP 失败: {url}, StatusCode={response.StatusCode}");
					continue;
				}

				// 阶段2：读取响应体（单独给60秒超时，避免内容读取超时）
				var contentLength = response.Content.Headers.ContentLength;
				LogHelper.WriteLogAsync($"[Network] 开始读取内容: {url}, Content-Length={contentLength}");

				// 使用流式读取 + 手动超时控制，避免大文件或慢速网络导致 ReadAsByteArrayAsync 超时
				using var contentCts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
				await using var responseStream = await response.Content.ReadAsStreamAsync(contentCts.Token).ConfigureAwait(false);
				using var ms = new MemoryStream();
				var buffer = new byte[8192];
				int read;
				while ((read = await responseStream.ReadAsync(buffer, contentCts.Token).ConfigureAwait(false)) > 0)
				{
					await ms.WriteAsync(buffer.AsMemory(0, read), contentCts.Token).ConfigureAwait(false);
				}
				var bytes = ms.ToArray();
				LogHelper.WriteLogAsync($"[Network] 下载成功: {url}, Content-Length={contentLength}, 实际大小={bytes.Length}");
				return bytes;
			}
			catch (OperationCanceledException ex)
			{
				if (ex.CancellationToken.IsCancellationRequested)
				{
					LogHelper.WriteLogAsync($"[Network] 请求超时/取消: {url}");
				}
				else
				{
					LogHelper.WriteLogAsync($"[Network] 请求被取消(非超时): {url}");
				}
			}
			catch (Exception ex)
			{
				LogHelper.WriteLogAsync($"[Network] 第 {attempt} 次尝试失败: {url}, {ex.GetType().Name}: {ex.Message}");
			}
		}

		LogHelper.WriteLogAsync($"[Network] 所有 {maxRetries + 1} 次尝试均失败: {url}");
		return null;
	}

	/// <summary>
	///     Attempts to load image from global cache (if it is stored before)
	/// </summary>
	/// <param name="url">Target url</param>
	/// <returns>Bitmap</returns>
	protected virtual Task<Bitmap?> LoadFromGlobalCache(string url)
	{
		// Current implementation does not provide global caching
		return Task.FromResult<Bitmap?>(null);
	}

	/// <summary>
	///     Attempts to load image from global cache (if it is stored before)
	/// </summary>
	/// <param name="url">Target url</param>
	/// <param name="imageBytes">Bytes to save</param>
	/// <returns>Bitmap</returns>
	protected virtual Task SaveToGlobalCache(string url, byte[] imageBytes)
	{
		// Current implementation does not provide global caching
		return Task.CompletedTask;
	}
}
