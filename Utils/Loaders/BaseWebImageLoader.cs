using System.Net;
using System.Net.Http.Headers;
using System.Net.Security;
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

	// 静态共享 HttpClient，复用连接池
	private static readonly HttpClient SharedClient = new(
		new SocketsHttpHandler
		{
			UseProxy = false,
			MaxConnectionsPerServer = 10,
			AllowAutoRedirect = true,
			SslOptions = new SslClientAuthenticationOptions { RemoteCertificateValidationCallback = (_, _, _, _) => true },
			AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
			PooledConnectionLifetime = TimeSpan.FromMinutes(5),
			EnableMultipleHttp2Connections = true,
		}
	)
	{
		Timeout = TimeSpan.FromSeconds(60),
	};

	static BaseWebImageLoader()
	{
		SharedClient.DefaultRequestHeaders.UserAgent.ParseAdd("pap.er/39 CFNetwork/3860.200.71 Darwin/25.1.0");
		SharedClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("image/avif"));
		SharedClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("image/webp"));
		SharedClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*") { Quality = 0.8 });
		SharedClient.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
		SharedClient.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("deflate"));
		SharedClient.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("br"));
	}

	/// <summary>
	///     Initializes a new instance with the provided <see cref="HttpClient" />, and specifies whether that
	///     <see cref="HttpClient" /> should be disposed when this instance is disposed.
	/// </summary>
	/// <param name="httpClient">The HttpMessageHandler responsible for processing the HTTP response messages.</param>
	/// <param name="disposeHttpClient">
	///     true if the inner handler should be disposed of by Dispose; false if you intend to
	///     reuse the HttpClient.
	/// </param>
	public BaseWebImageLoader()
	{
		_logger = Logger.TryGet(LogEventLevel.Information, "AsyncImageLoader");
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

		const int maxRetries = 3;
		const int initialDelay = 1000;
		Exception? lastException = null;

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
				using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
				using var request = new HttpRequestMessage(HttpMethod.Get, url);
				request.Headers.Host = "c3.wuse.co";

				using var response = await SharedClient.SendAsync(request, cts.Token).ConfigureAwait(false);

				if (response.IsSuccessStatusCode)
				{
					var contentLength = response.Content.Headers.ContentLength;
					var bytes = await response.Content.ReadAsByteArrayAsync(cts.Token);
					LogHelper.WriteLogAsync($"[Network] 下载成功: {url}, Content-Length={contentLength}, 实际大小={bytes.Length}");
					return bytes;
				}
				else
				{
					LogHelper.WriteLogAsync($"[Network] HTTP 失败: {url}, StatusCode={response.StatusCode}");
				}
			}
			catch (OperationCanceledException)
			{
				LogHelper.WriteLogAsync($"[Network] 请求超时/取消: {url}");
				lastException = new TimeoutException($"请求超时: {url}");
			}
			catch (Exception ex)
			{
				lastException = ex;
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
