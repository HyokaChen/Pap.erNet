using Pap.erNet.Utils;

namespace Pap.erNet.Tests;

public sealed class CdnResolverTests
{
	[Theory]
	[InlineData("img.paperapp.net", true)]
	[InlineData("c11.paperapp.net", true)]
	[InlineData("r2.paperapp.net", true)]
	[InlineData("c3.wuse.co", true)]
	[InlineData("IMG.PAPERAPP.NET", true)]
	[InlineData("img.paperapp.net.", true)]
	[InlineData("www.paperapp.net", false)]
	[InlineData("paperapp.net", false)]
	[InlineData("evil-paperapp.net", false)]
	[InlineData("img.paperapp.net.evil.com", false)]
	[InlineData(null, false)]
	[InlineData("", false)]
	[InlineData("   ", false)]
	public void IsImageCdnHost_RecognizesOnlyImageMirrors(string? host, bool expected)
	{
		Assert.Equal(expected, CdnResolver.IsImageCdnHost(host));
	}

	[Theory]
	[InlineData("https://img.paperapp.net/photos/a.jpg/thumb", "c11.paperapp.net", "https://c11.paperapp.net/photos/a.jpg/thumb")]
	[InlineData("https://r2.paperapp.net/photos/a.jpg/thumb", "img.paperapp.net", "https://img.paperapp.net/photos/a.jpg/thumb")]
	[InlineData("http://img.paperapp.net/photos/a.jpg/thumb", "c11.paperapp.net", "https://c11.paperapp.net/photos/a.jpg/thumb")]
	[InlineData("http://c3.wuse.co/photos/a.jpg/thumb?x=1&y=2", "c2.paperapp.net", "https://c2.paperapp.net/photos/a.jpg/thumb?x=1&y=2")]
	[InlineData("https://IMG.PAPERAPP.NET/photos/a.jpg/thumb", "c11.paperapp.net", "https://c11.paperapp.net/photos/a.jpg/thumb")]
	[InlineData(
		"https://img.paperapp.net/photos/a.jpg/thumb#preview",
		"c11.paperapp.net",
		"https://c11.paperapp.net/photos/a.jpg/thumb#preview"
	)]
	[InlineData("https://img.paperapp.net:443/photos/a.jpg/thumb", "c11.paperapp.net", "https://c11.paperapp.net/photos/a.jpg/thumb")]
	[InlineData("https://www.paperapp.net/status/cdn.txt", "c11.paperapp.net", "https://www.paperapp.net/status/cdn.txt")]
	[InlineData("https://example.com/photos/a.jpg/thumb", "c11.paperapp.net", "https://example.com/photos/a.jpg/thumb")]
	[InlineData(
		"https://img.paperapp.net.evil.com/photos/a.jpg/thumb",
		"c11.paperapp.net",
		"https://img.paperapp.net.evil.com/photos/a.jpg/thumb"
	)]
	[InlineData("https://evilpaperapp.net/photos/a.jpg/thumb", "c11.paperapp.net", "https://evilpaperapp.net/photos/a.jpg/thumb")]
	[InlineData("not a url", "c11.paperapp.net", "not a url")]
	[InlineData("/photos/a.jpg/thumb", "c11.paperapp.net", "/photos/a.jpg/thumb")]
	public void ResolveCdnUrl_RewritesOnlyImageMirrorHosts(string url, string? targetHost, string expected)
	{
		Assert.Equal(expected, CdnResolver.ResolveCdnUrl(url, targetHost));
	}

	[Fact]
	public void ResolveCdnUrl_WithoutResolvedHost_UpgradesHttpOnly()
	{
		Assert.Equal(
			"https://img.paperapp.net/photos/a.jpg/thumb",
			CdnResolver.ResolveCdnUrl("http://img.paperapp.net/photos/a.jpg/thumb", null)
		);
	}

	[Fact]
	public void ResolveCdnUrl_WithoutResolvedHost_LeavesHttpsUnchanged()
	{
		Assert.Equal(
			"https://img.paperapp.net/photos/a.jpg/thumb",
			CdnResolver.ResolveCdnUrl("https://img.paperapp.net/photos/a.jpg/thumb", null)
		);
	}

	[Fact]
	public void ResolveCdnUrl_SameHost_LeavesUrlUnchanged()
	{
		Assert.Equal(
			"https://img.paperapp.net/photos/a.jpg/thumb",
			CdnResolver.ResolveCdnUrl("https://img.paperapp.net/photos/a.jpg/thumb", "img.paperapp.net")
		);
	}

	[Theory]
	[InlineData("https://img.paperapp.net/photos/a.jpg/thumb", "/photos/a.jpg/thumb")]
	[InlineData("https://r2.paperapp.net/photos/a.jpg/thumb?x=1", "/photos/a.jpg/thumb?x=1")]
	[InlineData("https://c3.wuse.co/photos/a.jpg/thumb", "/photos/a.jpg/thumb")]
	[InlineData("https://c2.paperapp.net/photos/a.jpg/full", "/photos/a.jpg/full")]
	public void GetStableCacheKey_UsesPathAndQueryForImageMirrors(string url, string expected)
	{
		Assert.Equal(expected, CdnResolver.GetStableCacheKey(url));
	}

	[Theory]
	[InlineData("https://www.paperapp.net/status/cdn.txt")]
	[InlineData("https://example.com/photos/a.jpg/thumb")]
	[InlineData("not a url")]
	[InlineData("/photos/a.jpg/thumb")]
	public void GetStableCacheKey_LeavesNonImageUrlsUnchanged(string url)
	{
		Assert.Equal(url, CdnResolver.GetStableCacheKey(url));
	}

	[Theory]
	[InlineData("c11.paperapp.net\n", "c11.paperapp.net")]
	[InlineData("https://img.paperapp.net/anything", "img.paperapp.net")]
	[InlineData("# r2 fallback\nc2.paperapp.net\n", "c2.paperapp.net")]
	[InlineData("https://c3.wuse.co/foo", "c3.wuse.co")]
	public void ParseStatusHost_ReturnsFirstValidImageHost(string response, string expected)
	{
		Assert.Equal(expected, CdnResolver.ParseStatusHost(response));
	}

	[Theory]
	[InlineData("")]
	[InlineData("   \n\t ")]
	[InlineData("www.paperapp.net")]
	[InlineData("not a host")]
	[InlineData("https://example.com/cdn.txt")]
	public void ParseStatusHost_ReturnsNullWhenNoValidImageHost(string response)
	{
		Assert.Null(CdnResolver.ParseStatusHost(response));
	}
}
