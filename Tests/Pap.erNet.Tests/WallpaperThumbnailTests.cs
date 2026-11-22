using System.Text;
using Pap.erNet.Services;
using Sdcb.SimdCodecs.Webp;

namespace Pap.erNet.Tests;

public sealed class WallpaperThumbnailTests
{
	private const string SampleBlurhash = "LEHV6nWB2yk8pyo0adR*.7kCMdnj";

	[Fact]
	public void EncodeBlurhash_ValidHash_ReturnsDecodableWebpThumbnail()
	{
		var base64 = WallpaperListService.EncodeBlurhash(SampleBlurhash);

		Assert.False(string.IsNullOrEmpty(base64));

		var bytes = Convert.FromBase64String(base64);
		Assert.True(bytes.Length > 12);
		Assert.Equal("RIFF", Encoding.ASCII.GetString(bytes, 0, 4));
		Assert.Equal("WEBP", Encoding.ASCII.GetString(bytes, 8, 4));

		var image = WebpDecoder.Decode(bytes);
		Assert.Equal(560, image.Width);
		Assert.Equal(320, image.Height);
		Assert.False(image.HasAlpha);
		Assert.Equal(560 * 320 * 4, image.Rgba.Length);
	}

	[Theory]
	[InlineData("")]
	[InlineData("not-a-blurhash")]
	public void EncodeBlurhash_InvalidHash_ReturnsEmpty(string blurhash)
	{
		Assert.Equal(string.Empty, WallpaperListService.EncodeBlurhash(blurhash));
	}
}
