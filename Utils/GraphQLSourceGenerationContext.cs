using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Pap.erNet.Utils
{
	[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
	[JsonSerializable(typeof(PhotosGraphQL))]
	[JsonSerializable(typeof(VariablesGraphQL))]
	[JsonSerializable(typeof(Dictionary<string, string>))]
	public partial class GraphQLSourceGenerationContext : JsonSerializerContext { }
}
