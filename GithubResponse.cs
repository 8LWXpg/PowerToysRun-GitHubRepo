using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json.Serialization;
using System.Text.Json;
using Wox.Plugin.Logger;
using System.Windows.Navigation;

namespace Community.PowerToys.Run.Plugin.GithubRepo
{
    public struct GithubResponse
    {
        [JsonPropertyName("items")]
        public List<GithubRepo> Items { get; set; }
    }

    public struct GithubRepo
    {
        [JsonPropertyName("full_name")]
        public string FullName { get; set; }

        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("fork")]
        public bool Fork { get; set; }
    }

    public class QueryResult<T, E>
    {
        private readonly bool _success;
        private readonly T? Value;
        private readonly E? Exception;

        private QueryResult(T? v, E? e, bool success)
        {
            _success = success;
            Value = v;
            Exception = e;
        }

        public static QueryResult<T, E> Ok(T v) => new(v, default, true);
        public static QueryResult<T, E> Err(E e) => new(default, e, false);

        public static implicit operator bool(QueryResult<T, E> result) => result._success;
        public static implicit operator QueryResult<T, E>(T v) => new(v, default, true);
        public static implicit operator QueryResult<T, E>(E e) => new(default, e, false);

        public R Match<R>(Func<T, R> ok, Func<E, R> err) => _success ? ok(Value!) : err(Exception!);
    }

    public static class Github
    {
        private static readonly HttpClient Client;

        // Used to cancel the request if the user types a new query
        private static CancellationTokenSource? cts;

        static Github()
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
                Client.DefaultRequestHeaders.Add("Authorization", $"Bearer {auth}");
            }
        }

        public static async Task<QueryResult<GithubResponse, Exception>> RepoQuery(string query)
        {
            cts?.Cancel();
            cts = new CancellationTokenSource();

            return await SendRequest<GithubResponse>($"https://api.github.com/search/repositories?q={query}", cts.Token);
        }

        public static async Task<QueryResult<List<GithubRepo>, Exception>> UserRepoQuery(string user)
        {
            cts?.Cancel();
            cts = new CancellationTokenSource();

            // sort by latest update
            return await SendRequest<List<GithubRepo>>($"https://api.github.com/users/{user}/repos?sort=updated", cts.Token);
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