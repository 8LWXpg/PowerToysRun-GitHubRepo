using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Wox.Plugin.Logger;

namespace Community.PowerToys.Run.Plugin.GitHubRepo
{
    public record GitHubResponse
    {
        [JsonPropertyName("items")]
        public List<GitHubRepo> Items { get; init; }
        public GitHubResponse(List<GitHubRepo> items) => Items = items;
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
        private static CancellationTokenSource? cts;
        private static string _url = "https://api.github.com";

        public static string Url
        {
            get => _url;
            set => _url = string.IsNullOrEmpty(value) ? "https://api.github.com" : value;
        }

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
                _ = Client.DefaultRequestHeaders.Remove("Authorization");
            }
            else
            {
                _ = Client.DefaultRequestHeaders.Remove("Authorization");
                Client.DefaultRequestHeaders.Add("Authorization", $"Bearer {auth}");
            }
        }

        public static async Task<QueryResult<GitHubResponse, Exception>> RepoQuery(
            string query,
            string sort = "stars",
            string order = "desc",
            string language = null)
        {
            cts?.Cancel();
            cts = new CancellationTokenSource();
            string searchQuery = query;
            if (!string.IsNullOrEmpty(language))
            {
                searchQuery += $" language:{language}";
            }
            string url = $"{_url}/search/repositories?q={Uri.EscapeDataString(searchQuery)}";
            if (!string.IsNullOrEmpty(sort))
            {
                url += $"&sort={Uri.EscapeDataString(sort)}";
            }
            if (!string.IsNullOrEmpty(order))
            {
                url += $"&order={Uri.EscapeDataString(order)}";
            }
            return await SendRequest<GitHubResponse>(url, cts.Token);
        }

        public static async Task<QueryResult<List<GitHubRepo>, Exception>?> UserRepoQuery(string user)
        {
            cts?.Cancel();
            cts = new CancellationTokenSource();
            try
            {
                return await SendRequest<List<GitHubRepo>>($"{_url}/users/{user}/repos?sort=updated", cts.Token);
            }
            catch
            {
                return null;
            }
        }

        public static async Task<QueryResult<List<GitHubRepo>, Exception>?> UserTokenQuery()
        {
            cts?.Cancel();
            cts = new CancellationTokenSource();
            try
            {
                return await SendRequest<List<GitHubRepo>>($"{_url}/user/repos?sort=updated", cts.Token);
            }
            catch
            {
                return null;
            }
        }

        private static async Task<QueryResult<T, Exception>> SendRequest<T>(string url, CancellationToken token)
        {
            try
            {
                HttpResponseMessage responseMessage = await Client.GetAsync(url, token);
                _ = responseMessage.EnsureSuccessStatusCode();
                var json = await responseMessage.Content.ReadAsStringAsync(token);
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
