using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
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
		var internalOrCachedBitmap =
			await LoadFromLocalAsync(url).ConfigureAwait(false)
			?? await LoadFromInternalAsync(url).ConfigureAwait(false)
			?? await LoadFromGlobalCache(url).ConfigureAwait(false);
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
			Debug.WriteLine($"Request Error::{ex.Message}");
			throw ex;
		}
	}

	/// <summary>
	/// the url maybe is local file url,so if file exists ,we got a Bitmap
	/// </summary>
	/// <param name="url"></param>
	/// <returns></returns>
	private Task<Bitmap?> LoadFromLocalAsync(string url)
	{
		return Task.FromResult(File.Exists(url) ? new Bitmap(url) : null);
	}

	/// <summary>
	///     Receives image bytes from an internal source (for example, from the disk).
	///     This data will be NOT cached globally (because it is assumed that it is already in internal source us and does not
	///     require global caching)
	/// </summary>
	/// <param name="url">Target url</param>
	/// <returns>Bitmap</returns>
	protected virtual Task<Bitmap?> LoadFromInternalAsync(string url)
	{
		try
		{
			var uri = url.StartsWith("/") ? new Uri(url, UriKind.Relative) : new Uri(url, UriKind.RelativeOrAbsolute);

			if (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
				return Task.FromResult<Bitmap?>(null);

			if (uri is { IsAbsoluteUri: true, IsFile: true })
				return Task.FromResult(new Bitmap(uri.LocalPath))!;

			return Task.FromResult(new Bitmap(AssetLoader.Open(uri)))!;
		}
		catch (Exception e)
		{
			_logger?.Log(this, "Failed to resolve image from request with uri: {RequestUri}\nException: {Exception}", url, e);
			throw e;
			//return Task.FromResult<Bitmap?>(null);
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
		try
		{
			Debug.WriteLine($"Thumb Url::{url}");
			var client = new HttpClient(
				new SocketsHttpHandler()
				{
					UseProxy = false,
					MaxConnectionsPerServer = 10,
					AllowAutoRedirect = false,
					SslOptions = new SslClientAuthenticationOptions()
					{
						RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true
					},
					AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
				}
			)
			{
				Timeout = TimeSpan.FromSeconds(300),
			};
			client.DefaultRequestHeaders.Add("Host", "c3.wuse.co");
			client.DefaultRequestHeaders.Add("Accept-Language", "zh-CN,zh-Hans;q=0.9");
			client.DefaultRequestHeaders.Add("Accept", "image/avif,image/webp;q=0.9,*/*");
			client.DefaultRequestHeaders.Add(
				"User-Agent",
				"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36"
			);
			return await client.GetByteArrayAsync(url).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			throw ex;
		}
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
