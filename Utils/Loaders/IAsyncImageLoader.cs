using Avalonia.Media.Imaging;

namespace Pap.erNet.Utils.Loaders;

public interface IAsyncImageLoader : IDisposable
{
	/// <summary>
	///     Loads image
	/// </summary>
	/// <param name="url">Target url；允许任意 CDN 镜像域名，实现负责在下载前解析到当前可用域名</param>
	/// <param name="cancellationToken">取消令牌，可中断在途下载</param>
	/// <returns>Bitmap</returns>
	public Task<Bitmap?> ProvideImageAsync(string url, CancellationToken cancellationToken = default);
}
