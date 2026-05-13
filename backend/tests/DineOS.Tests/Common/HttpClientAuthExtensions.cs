using System.Net.Http.Headers;

namespace DineOS.Tests.Common;

public static class HttpClientAuthExtensions
{
    public static HttpClient WithBearer(this HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
