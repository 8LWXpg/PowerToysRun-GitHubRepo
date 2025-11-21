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
	private static readonly Dictionary<string, HttpClient> _clientsByUsername = [];
	private static readonly HttpClient _defaultClient = CreateClient(null);

	// Used to cancel the request if the user types a new query
	private static CancellationTokenSource? _cts;
	private static string _url = "https://api.github.com";
	public static string Url
	{
		get => _url;
		set => _url = string.IsNullOrWhiteSpace(value) ? "https://api.github.com" : value;
	}
	public static int PageSize { get; set; }

	private static HttpClient CreateClient(string? token)
	{
		var client = new HttpClient();
		client.DefaultRequestHeaders.UserAgent.Add(ProductInfoHeaderValue.Parse("PowerToys"));
		client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
		client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");

		if (!string.IsNullOrEmpty(token))
		{
			client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
		}

		return client;
	}

	/// <summary>
	/// Update <c>_clientsByUsername</c> with new usernames and <c>HttpClient</c> with token
	/// </summary>
	public static void UpdateAuth(IReadOnlyList<string> usernames, IReadOnlyList<string> tokens)
	{
		foreach (HttpClient client in _clientsByUsername.Values)
		{
			client.Dispose();
		}

		_clientsByUsername.Clear();

		var count = Math.Min(usernames.Count, tokens.Count);
		for (var i = 0; i < count; i++)
		{
			var username = usernames[i];
			var token = tokens[i];
			_clientsByUsername[username] = CreateClient(token);
		}
	}

	private static HttpClient GetAnyClient() => _clientsByUsername.Values.FirstOrDefault() ?? _defaultClient;
	private static HttpClient GetClientByUsername(string username) => _clientsByUsername.TryGetValue(username, out HttpClient? client) ? client : _defaultClient;

	public static async Task<List<GitHubRepo>> RepoQuery(string query)
	{
		_cts?.Cancel();
		_cts = new CancellationTokenSource();

		QueryResult<GitHubResponse, Exception> result = await SendRequest<GitHubResponse>(
			GetAnyClient(),
			$"{_url}/search/repositories?per_page={PageSize}&q={query}",
			_cts.Token
		);

		return result.Match(
			ok => ok.Items,
			err => [new(err.GetType().Name, string.Empty, err.Message, false)]
		);
	}

	public static async Task<List<GitHubRepo>> UserRepoQuery(string user)
	{
		_cts?.Cancel();
		_cts = new CancellationTokenSource();

		QueryResult<List<GitHubRepo>, Exception> result = _clientsByUsername.TryGetValue(user, out HttpClient? client)
			? await SendRequest<List<GitHubRepo>>(
				client,
				$"{_url}/user/repos?per_page={PageSize}&sort=updated",
				_cts.Token
			)
			: await SendRequest<List<GitHubRepo>>(
				_defaultClient,
				$"{_url}/users/{user}/repos?per_page={PageSize}&sort=updated",
				_cts.Token
			);

		return result.Match(
			ok => ok,
			err => [new(err.GetType().Name, string.Empty, err.Message, false)]
		);
	}

	private static async Task<QueryResult<T, Exception>> SendRequest<T>(HttpClient client, string url, CancellationToken token)
	{
		try
		{
			HttpResponseMessage responseMessage = await client.GetAsync(url, token);
			_ = responseMessage.EnsureSuccessStatusCode();
			var json = await responseMessage.Content.ReadAsStringAsync(token);
			T response = JsonSerializer.Deserialize<T>(json)!;
			return response;
		}
		catch (OperationCanceledException e)
		{
			// Not logging this
			return e;
		}
		catch (Exception e)
		{
			Log.Error(e.Message, typeof(Main));
			return e;
		}
	}
}
