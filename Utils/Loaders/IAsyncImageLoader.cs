using Avalonia.Media.Imaging;

namespace Pap.erNet.Utils.Loaders;

public interface IAsyncImageLoader : IDisposable
{
	/// <summary>
	///     Loads image
	/// </summary>
	/// <param name="url">Target url</param>
	/// <param name="cancellationToken">取消令牌，可中断在途下载</param>
	/// <returns>Bitmap</returns>
	public Task<Bitmap?> ProvideImageAsync(string url, CancellationToken cancellationToken = default);
}
