using System.Text.Json.Serialization;

namespace Pap.erNet.Models;

/// <summary>
/// Lists GraphQL 请求体
/// </summary>
public class ListsGraphQL
{
	public required string Query { get; set; }
	public required string OperationName { get; set; }
	public object? Variables { get; set; }
}

/// <summary>
/// Lists 接口响应
/// </summary>
public class ListsResponse
{
	public required ListsData Data { get; set; }
}

public class ListsData
{
	public required List<SimpleList> Lists { get; set; }
}

public class SimpleList
{
	[JsonPropertyName("__typename")]
	public required string Typename { get; set; }

	public required string Id { get; set; }
	public required string Name { get; set; }
	public required string Type { get; set; }
	public string? Link { get; set; }
	public required int Position { get; set; }
	public string? Description { get; set; }
}
