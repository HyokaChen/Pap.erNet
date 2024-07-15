using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Pap.erNet.Models;

public class Wallpaper
{
	public string Url { get; set; } = string.Empty;

	public string Thumbnail { get; set; } = string.Empty;
	public string ResolutionRatio { get; set; } = string.Empty;
	public string Author { get; set; } = string.Empty;

	public string Link { get; set; } = string.Empty;

	public string Id { get; set; } = string.Empty;
}

public class ResponseType
{
	public required Data Data { get; set; }
}

public class Data
{
	public required Photos Photos { get; set; }
}

public class Photos
{
	[JsonPropertyName("__typename")]
	public required string __Typename { get; set; }
	public string? After { get; set; }
	public string? Before { get; set; }
	public required List<Entry> Entries { get; set; }

	public string? ListId { get; set; }
}

public class Entry
{
	[JsonPropertyName("__typename")]
	public required string __Typename { get; set; }

	[JsonPropertyName("blurHash")]
	public required string Blurhash { get; set; }
	public required string Color { get; set; }
	public required string Creator { get; set; }
	public required string Heading { get; set; }

	public required int Width { get; set; }
	public required int Height { get; set; }

	public required string Id { get; set; }
	public required string Link { get; set; }
	public required bool Linkable { get; set; }
	public string? Type { get; set; }

	public required PhotoUrl Urls { get; set; }
}

public class PhotoUrl
{
	[JsonPropertyName("__typename")]
	public required string __Typename { get; set; }
	public required string Thumb { get; set; }
}
