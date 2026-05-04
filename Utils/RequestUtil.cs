using System.Net.Http.Headers;
using System.Net.Security;
using System.Text;
using System.Text.Json;
using Pap.erNet.Models;

namespace Pap.erNet.Utils;

public static class RequestUtil
{
	private const string PAPER_URL = "https://paper.nsns.in/graphql";

	private static string GraphQLQuery =>
		"""
			query Photos($after: String, $before: String, $listId: ID, $filters: PhotosFiltersInput) {
			  photos(after: $after, before: $before, listId: $listId, filters: $filters) {
			    __typename
			    after
			    before
			    listId
			    entries {
			      __typename
			      id
			      type
			      color
			      blurHash
			      creator
			      urls {
			        __typename
			        thumb
			      }
			      width
			      height
			      link
			      linkable
			      heading
			    }
			  }
			}
			""";

	private static string ListsQuery =>
		"""
			query Lists {
			  lists {
			    __typename
			    id
			    name
			    type
			    link
			    position
			    description
			  }
			}
			""";

	private static HttpClient PhotosHttpClient { get; } =
		new(
			new SocketsHttpHandler
			{
				UseProxy = false,
				AllowAutoRedirect = true,
				SslOptions = new SslClientAuthenticationOptions
				{
					RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true,
				},
			}
		)
		{
			Timeout = TimeSpan.FromSeconds(30),
		};

	static RequestUtil()
	{
		PhotosHttpClient.DefaultRequestHeaders.Host = "paper.nsns.in";
		PhotosHttpClient.DefaultRequestHeaders.Connection.Add("keep-alive");
		PhotosHttpClient.DefaultRequestHeaders.Add("locale", "zh-Hans");
		PhotosHttpClient.DefaultRequestHeaders.Add("client-version", "39.0");
		PhotosHttpClient.DefaultRequestHeaders.Add("apollographql-client-version", "5.3.0-39");
		PhotosHttpClient.DefaultRequestHeaders.Add("apollographql-client-name", "com.w.paper.apollo-ios");
		PhotosHttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("pap.er/39 CFNetwork/3860.200.71 Darwin/25.1.0");
	}

	/// <summary>
	/// 获取 Photos 数据（自动附加认证头）
	/// </summary>
	public static async Task<ResponseType?> GetResponse(string listId, string? after = null, string? before = null)
	{
		try
		{
			var photosQueryRequest = new PhotosGraphQL
			{
				Query = GraphQLQuery,
				OperationName = "Photos",
				Variables = new VariablesGraphQL
				{
					ListId = listId,
					After = after,
					Before = before,
				},
			};

			var jsonString = JsonSerializer.Serialize(photosQueryRequest!, GraphQLSourceGenerationContext.Default.PhotosGraphQL);
			var requestMessage = new HttpRequestMessage(HttpMethod.Post, PAPER_URL)
			{
				Content = new StringContent(jsonString, Encoding.UTF8, new MediaTypeHeaderValue("application/json")),
			};

			// 动态请求头：did 和认证信息
			var deviceUid = DeviceUtil.GetDeviceUid();
			requestMessage.Headers.Add("did", deviceUid);
			requestMessage.Headers.Add("X-APOLLO-OPERATION-NAME", "Photos");
			requestMessage.Headers.Add("X-APOLLO-OPERATION-TYPE", "query");

			// 附加 Authorization
			var authHeader = AuthService.Instance.GetAuthorizationHeader();
			if (authHeader != null)
			{
				requestMessage.Headers.Add("Authorization", authHeader);
			}

			using var httpResponseMessage = await PhotosHttpClient.SendAsync(requestMessage).ConfigureAwait(false);
			var respStr = await httpResponseMessage.Content.ReadAsStringAsync().ConfigureAwait(false);

			if (!httpResponseMessage.IsSuccessStatusCode)
			{
				LogHelper.WriteLogAsync($"RequestUtil.GetResponse: 请求失败, StatusCode={httpResponseMessage.StatusCode}, Body={respStr}");

				// 如果是 401，尝试重新认证
				if (httpResponseMessage.StatusCode == System.Net.HttpStatusCode.Unauthorized)
				{
					LogHelper.WriteLogAsync("RequestUtil.GetResponse: 收到 401，尝试重新认证...");
					var reAuth = await AuthService.Instance.AuthenticateAsync().ConfigureAwait(false);
					if (reAuth)
					{
						// 重新认证成功，重试一次请求（避免无限递归）
						return await GetResponseInternal(listId, after, before).ConfigureAwait(false);
					}
				}

				return null;
			}

			return JsonSerializer.Deserialize(respStr, ResponseSourceGenerationContext.Default.ResponseType);
		}
		catch (Exception ex)
		{
			LogHelper.WriteLogAsync($"RequestUtil.GetResponse: 异常 - {ex.Message}\n{ex.StackTrace}");
			throw;
		}
	}

	/// <summary>
	/// 内部重试方法（不带 401 重试逻辑，避免无限递归）
	/// </summary>
	private static async Task<ResponseType?> GetResponseInternal(string listId, string? after = null, string? before = null)
	{
		var photosQueryRequest = new PhotosGraphQL
		{
			Query = GraphQLQuery,
			OperationName = "Photos",
			Variables = new VariablesGraphQL
			{
				ListId = listId,
				After = after,
				Before = before,
			},
		};

		var jsonString = JsonSerializer.Serialize(photosQueryRequest!, GraphQLSourceGenerationContext.Default.PhotosGraphQL);
		var requestMessage = new HttpRequestMessage(HttpMethod.Post, PAPER_URL)
		{
			Content = new StringContent(jsonString, Encoding.UTF8, new MediaTypeHeaderValue("application/json")),
		};

		var deviceUid = DeviceUtil.GetDeviceUid();
		requestMessage.Headers.Add("did", deviceUid);
		requestMessage.Headers.Add("X-APOLLO-OPERATION-NAME", "Photos");
		requestMessage.Headers.Add("X-APOLLO-OPERATION-TYPE", "query");

		var authHeader = AuthService.Instance.GetAuthorizationHeader();
		if (authHeader != null)
		{
			requestMessage.Headers.Add("Authorization", authHeader);
		}

		using var httpResponseMessage = await PhotosHttpClient.SendAsync(requestMessage).ConfigureAwait(false);
		var respStr = await httpResponseMessage.Content.ReadAsStringAsync().ConfigureAwait(false);

		return JsonSerializer.Deserialize(respStr, ResponseSourceGenerationContext.Default.ResponseType);
	}

	/// <summary>
	/// 获取 Lists 分类列表（自动附加认证头）
	/// </summary>
	public static async Task<ListsResponse?> GetListsAsync()
	{
		try
		{
			var listsRequest = new ListsGraphQL
			{
				Query = ListsQuery,
				OperationName = "Lists",
				Variables = null,
			};

			var jsonString = JsonSerializer.Serialize(listsRequest, ListsSourceGenerationContext.Default.ListsGraphQL);
			var requestMessage = new HttpRequestMessage(HttpMethod.Post, PAPER_URL)
			{
				Content = new StringContent(jsonString, Encoding.UTF8, new MediaTypeHeaderValue("application/json")),
			};

			var deviceUid = DeviceUtil.GetDeviceUid();
			requestMessage.Headers.Add("did", deviceUid);
			requestMessage.Headers.Add("X-APOLLO-OPERATION-NAME", "Lists");
			requestMessage.Headers.Add("X-APOLLO-OPERATION-TYPE", "query");

			var authHeader = AuthService.Instance.GetAuthorizationHeader();
			if (authHeader != null)
			{
				requestMessage.Headers.Add("Authorization", authHeader);
			}

			using var httpResponseMessage = await PhotosHttpClient.SendAsync(requestMessage).ConfigureAwait(false);
			var respStr = await httpResponseMessage.Content.ReadAsStringAsync().ConfigureAwait(false);

			if (!httpResponseMessage.IsSuccessStatusCode)
			{
				LogHelper.WriteLogAsync($"RequestUtil.GetLists: 请求失败, StatusCode={httpResponseMessage.StatusCode}");
				return null;
			}

			return JsonSerializer.Deserialize(respStr, ListsSourceGenerationContext.Default.ListsResponse);
		}
		catch (Exception ex)
		{
			LogHelper.WriteLogAsync($"RequestUtil.GetLists: 异常 - {ex.Message}\n{ex.StackTrace}");
			return null;
		}
	}
}

public class PhotosGraphQL
{
	public required string Query { get; set; }
	public required string OperationName { get; set; }

	public VariablesGraphQL Variables { get; set; } = new();
}

public class VariablesGraphQL
{
	public string ListId { get; set; } = string.Empty;
	public string? After { get; set; }
	public string? Before { get; set; }

	public Dictionary<string, string> Filters { get; set; } = new();
}

/// <summary>
/// Lists 相关模型的 JSON 序列化上下文（Source Generator）
/// </summary>
[System.Text.Json.Serialization.JsonSourceGenerationOptions(
	WriteIndented = true,
	PropertyNamingPolicy = System.Text.Json.Serialization.JsonKnownNamingPolicy.CamelCase
)]
[System.Text.Json.Serialization.JsonSerializable(typeof(ListsGraphQL))]
[System.Text.Json.Serialization.JsonSerializable(typeof(ListsResponse))]
[System.Text.Json.Serialization.JsonSerializable(typeof(ListsData))]
[System.Text.Json.Serialization.JsonSerializable(typeof(SimpleList))]
public partial class ListsSourceGenerationContext : System.Text.Json.Serialization.JsonSerializerContext { }
