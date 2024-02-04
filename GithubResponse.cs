using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json.Serialization;
using System.Text.Json;
using Wox.Plugin.Logger;

namespace Community.PowerToys.Run.Plugin.GitHubRepo
{
    public record GitHubResponse
    {
        [JsonPropertyName("items")]
        public List<GitHubRepo> Items { get; init; }

        public GitHubResponse(List<GitHubRepo> items)
        {
            Items = items;
        }
    }

    public record GitHubRepo
    {
        [JsonPropertyName("full_name")]
        public string FullName { get; init; }

        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; init; }

        [JsonPropertyName("description")]
        public string Description { get; init; }

        [JsonPropertyName("fork")]
        public bool Fork { get; init; }

        public GitHubRepo(string fullName, string htmlUrl, string description, bool fork)
        {
            FullName = fullName;
            HtmlUrl = htmlUrl;
            Description = description;
            Fork = fork;
        }
    }

    public static class GitHub
    {
        private static readonly HttpClient Client;

        // Used to cancel the request if the user types a new query
        private static CancellationTokenSource? cts;

        static GitHub()
        {
            Client = new HttpClient();
            Client.DefaultRequestHeaders.UserAgent.Add(ProductInfoHeaderValue.Parse("PowerToys"));
            Client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
            Client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        }

        public static void UpdateAuthSetting(string auth)
        {
            if (string.IsNullOrEmpty(auth))
            {
                Client.DefaultRequestHeaders.Remove("Authorization");
            }
            else
            {
                Client.DefaultRequestHeaders.Remove("Authorization");
                Client.DefaultRequestHeaders.Add("Authorization", $"Bearer {auth}");
            }
        }

        public static async Task<QueryResult<GitHubResponse, Exception>> RepoQuery(string query)
        {
            cts?.Cancel();
            cts = new CancellationTokenSource();

            return await SendRequest<GitHubResponse>($"https://api.github.com/search/repositories?q={query}", cts.Token);
        }

        public static async Task<QueryResult<List<GitHubRepo>, Exception>> UserRepoQuery(string user)
        {
            cts?.Cancel();
            cts = new CancellationTokenSource();

            // sort by latest update, only works if your target is top 30 that recently updated
            return await SendRequest<List<GitHubRepo>>($"https://api.github.com/users/{user}/repos?sort=updated", cts.Token);
        }

        private static async Task<QueryResult<T, Exception>> SendRequest<T>(string url, CancellationToken token)
        {
            try
            {
                HttpResponseMessage responseMessage = await Client.GetAsync(url, token);
                _ = responseMessage.EnsureSuccessStatusCode();
                string json = await responseMessage.Content.ReadAsStringAsync(token);
                T? response = JsonSerializer.Deserialize<T>(json);
                return response!;
            }
            catch (Exception e)
            {
                Log.Error(e.Message, typeof(Main));
                return e;
            }
        }
    }
}
