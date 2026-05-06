namespace Pap.erNet.Utils;

public static class AppConstants
{
	public const string APP_VERSION = "5.4.2";
	public const int APP_BUILD = 50;
	public const string APOLLO_CLIENT_NAME = "com.w.paper-apollo-ios";

	public static string ApolloClientVersion => $"{APP_VERSION}-{APP_BUILD}";
	public static string ClientVersion => $"{APP_BUILD}.0";
}
