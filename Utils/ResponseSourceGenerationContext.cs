using System.Collections.Generic;
using System.Text.Json.Serialization;
using Pap.erNet.Models;

namespace Pap.erNet.Utils
{
	[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
	[JsonSerializable(typeof(ResponseType))]
	[JsonSerializable(typeof(Data))]
	[JsonSerializable(typeof(Photos))]
	[JsonSerializable(typeof(List<Entry>))]
	[JsonSerializable(typeof(Entry))]
	[JsonSerializable(typeof(PhotoUrl))]
	public partial class ResponseSourceGenerationContext : JsonSerializerContext { }
}
