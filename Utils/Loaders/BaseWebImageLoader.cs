using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Threading.Tasks;
using Avalonia.Logging;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Pap.erNet.Utils.Loaders;

/// <summary>
///     Provides non cached way to asynchronously load images for <see cref="ImageLoader" />
///     Can be used as base class if you want to create custom caching mechanism
/// </summary>
public class BaseWebImageLoader : IAsyncImageLoader
{
	private readonly ParametrizedLogger? _logger;

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
		_logger = Logger.TryGet(LogEventLevel.Information, ImageLoader.AsyncImageLoaderLogArea);
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
			LogHelper.WriteLogAsync($"Request Error::{ex.Message} >>> {ex.StackTrace}");
			throw ex;
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
		LogHelper.WriteLogAsync($"Thumb Url::{url}");
		var client = new HttpClient(
			new SocketsHttpHandler()
			{
				UseProxy = false,
				MaxConnectionsPerServer = 5,
				AllowAutoRedirect = true,
				SslOptions = new SslClientAuthenticationOptions()
				{
					RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true,
				},
				AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
			}
		)
		{
			Timeout = TimeSpan.FromSeconds(300),
		};
		client.DefaultRequestHeaders.UserAgent.ParseAdd(
			"Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/103.0.0.0 Safari/537.36"
		);
		client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("image/avif"));
		client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("image/webp"));
		// 添加带 q-value 的媒体类型
		client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*") { Quality = 0.8 });
		client.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
		client.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("deflate"));
		client.DefaultRequestHeaders.Host = "c3.wuse.co";

		var maxRetries = 3;
		var initialDelay = 1000;
		Exception? lastException = null;

		for (var attempt = 0; attempt <= maxRetries; attempt++)
		{
			if (attempt > 0)
			{
				var delay = (int)(initialDelay * Math.Pow(2, attempt - 1));
				await Task.Delay(delay);
			}

			try
			{
				// 每次循环都重新创建请求和内容
				using var response = await client.GetAsync(url).ConfigureAwait(false);

				if (response.IsSuccessStatusCode)
				{
					return await response.Content.ReadAsByteArrayAsync();
				}
			}
			catch (HttpRequestException ex)
			{
				lastException = ex;
				Console.WriteLine($"[POST重试] 第 {attempt} 次尝试失败: {ex.Message}");
			}
		}
		throw new HttpRequestException($"POST请求所有 {maxRetries + 1} 次尝试均失败。", lastException);
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
