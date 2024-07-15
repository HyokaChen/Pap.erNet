﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Pap.erNet.Models;

namespace Pap.erNet.Utils
{
	public static class RequestUtil
	{
		public const string PAPER_URL = "https://paper.nsns.in/graphql";
		public static string GraphQLQuery { get; set; } =
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
		public static HttpClient PhotosHttpClient { get; set; } = new() { Timeout = TimeSpan.FromSeconds(30) };

		static RequestUtil()
		{
			PhotosHttpClient.DefaultRequestHeaders.Add("X-APOLLO-OPERATION-NAME", "Photos");
			PhotosHttpClient.DefaultRequestHeaders.Add("apollographql-client-name", "com.w.paper-apollo-ios");
			PhotosHttpClient.DefaultRequestHeaders.Add("User-Agent", "pap.er/39");
		}

		public static async Task<ResponseType?> GetResponse(string listId)
		{
			try
			{
				var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
				var photosQueryRequest = new PhotosGraphQL
				{
					Query = GraphQLQuery,
					OperationName = "Photos",
					Variables = new()
					{
						ListId = listId,
						After = (string)null,
						Before = (string)null,
					}
				};
				var jsonString = JsonSerializer.Serialize(photosQueryRequest!, GraphQLSourceGenerationContext.Default.PhotosGraphQL);
				var postContent = new StringContent(jsonString, Encoding.UTF8, new MediaTypeHeaderValue("application/json"));
				using var httpResponseMessage = await PhotosHttpClient.PostAsync(PAPER_URL, postContent).ConfigureAwait(false);
				var respStr = await httpResponseMessage.Content.ReadAsStringAsync();
				return JsonSerializer.Deserialize(respStr, ResponseSourceGenerationContext.Default.ResponseType);
			}
			catch (Exception ex)
			{
				Debug.WriteLine(ex);
				throw ex;
			}
		}
	}

	public class PhotosGraphQL
	{
		public required string Query { get; set; }
		public required string OperationName { get; set; }

		public VariablesGraphQL Variables { get; set; }
	}

	public class VariablesGraphQL
	{
		public required string ListId { get; set; }
		public string? After { get; set; }
		public string? Before { get; set; }

		public Dictionary<string, string> Filters { get; set; } = new();
	}
}
