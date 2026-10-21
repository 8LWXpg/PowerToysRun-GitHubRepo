using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Wox.Plugin.Logger;

namespace Community.PowerToys.Run.Plugin.GitHubRepo;

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
	private static readonly HttpClient _client = CreateClient();
	private static Dictionary<string, string> _tokensByUsername = [];

	// Used to cancel the request if the user types a new query
	private static CancellationTokenSource? _cts;

	public static string Url
	{
		get;
		set => field = string.IsNullOrWhiteSpace(value) ? "https://api.github.com" : value;
	} = "https://api.github.com";
	public static int PageSize { get; set; }

	private static HttpClient CreateClient()
	{
		var client = new HttpClient();
		client.DefaultRequestHeaders.UserAgent.Add(ProductInfoHeaderValue.Parse("PowerToys"));
		client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
		client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
		return client;
	}

	public static void UpdateAuth(IReadOnlyList<string> usernames, IReadOnlyList<string> tokens)
		=> _tokensByUsername = usernames.Zip(tokens).ToDictionary();

	public static async Task<List<GitHubRepo>> RepoQuery(string query)
	{
		_cts?.Cancel();
		_cts?.Dispose();
		_cts = new CancellationTokenSource();

		QueryResult<GitHubResponse, Exception>? result = await SendRequest<GitHubResponse>(
			_tokensByUsername.FirstOrDefault().Value,
			$"{Url}/search/repositories?per_page={PageSize}&q={query}",
			_cts.Token
		);

		return result?.Match(
			ok => ok.Items,
			err => [new(err.GetType().Name, string.Empty, err.Message, false)]
		) ?? [];
	}

	public static Task<List<GitHubRepo>> DefaultUserQuery(string user) =>
		_tokensByUsername.ContainsKey(user) ? TokenQuery(user) : UserRepoQuery(user);

	public static async Task<List<GitHubRepo>> TokenQuery(string user)
	{
		_cts?.Cancel();
		_cts?.Dispose();
		_cts = new CancellationTokenSource();

		QueryResult<List<GitHubRepo>, Exception>? result = await SendRequest<List<GitHubRepo>>(
				_tokensByUsername[user],
				$"{Url}/user/repos?per_page={PageSize}&sort=updated",
				_cts.Token
			);

		return result?.Match(
			ok => ok,
			err => [new(err.GetType().Name, string.Empty, err.Message, false)]
		) ?? [];
	}

	public static async Task<List<GitHubRepo>> UserRepoQuery(string user)
	{
		_cts?.Cancel();
		_cts?.Dispose();
		_cts = new CancellationTokenSource();

		QueryResult<List<GitHubRepo>, Exception>? result = await SendRequest<List<GitHubRepo>>(
				_tokensByUsername.FirstOrDefault().Value,
				$"{Url}/users/{user}/repos?per_page={PageSize}&sort=updated",
				_cts.Token
			);

		return result?.Match(
			ok => ok,
			err => [new(err.GetType().Name, string.Empty, err.Message, false)]
		) ?? [];
	}

	private static async Task<QueryResult<T, Exception>?> SendRequest<T>(string? token, string url, CancellationToken ct)
	{
		try
		{
			using var request = new HttpRequestMessage(HttpMethod.Get, url);
			if (!string.IsNullOrEmpty(token))
			{
				request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
			}

			using HttpResponseMessage response = await _client.SendAsync(request, ct);
			_ = response.EnsureSuccessStatusCode();
			var json = await response.Content.ReadAsStringAsync(ct);
			return JsonSerializer.Deserialize<T>(json)!;
		}
		catch (OperationCanceledException)
		{
			// Not logging this
			return null;
		}
		catch (Exception e)
		{
			Log.Error(e.Message, typeof(Main));
			return e;
		}
	}
}
