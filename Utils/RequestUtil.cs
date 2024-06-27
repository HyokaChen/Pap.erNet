using System.Net.Http;
using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.SystemTextJson;

namespace Pap.erNet.Utils
{
    public static class RequestUtil
    {
        public static GraphQLHttpClient GraphQLClient { get; set; } =
            new("https://paper.nsns.in/graphql", new SystemTextJsonSerializer());

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
    }
}
