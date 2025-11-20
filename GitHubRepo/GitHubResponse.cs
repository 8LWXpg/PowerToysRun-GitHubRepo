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
	private static readonly HttpClient _client;

	// Used to cancel the request if the user types a new query
	private static CancellationTokenSource? _cts;
	private static string _url = "https://api.github.com";
	public static string Url
	{
		get => _url;
		set => _url = string.IsNullOrWhiteSpace(value) ? "https://api.github.com" : value;
	}

	static GitHub()
	{
		_client = new HttpClient();
		_client.DefaultRequestHeaders.UserAgent.Add(ProductInfoHeaderValue.Parse("PowerToys"));
		_client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
		_client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
	}

	public static void UpdateAuthSetting(string auth)
	{
		if (string.IsNullOrEmpty(auth))
		{
			_ = _client.DefaultRequestHeaders.Remove("Authorization");
		}
		else
		{
			_ = _client.DefaultRequestHeaders.Remove("Authorization");
			_client.DefaultRequestHeaders.Add("Authorization", $"Bearer {auth}");
		}
	}

	public static async Task<QueryResult<GitHubResponse, Exception>> RepoQuery(string query, int pageSize)
	{
		_cts?.Cancel();
		_cts = new CancellationTokenSource();

		return await SendRequest<GitHubResponse>($"{_url}/search/repositories?per_page={pageSize}&q={query}", _cts.Token);
	}

	public static async Task<QueryResult<List<GitHubRepo>, Exception>?> UserRepoQuery(string user, int pageSize)
	{
		_cts?.Cancel();
		_cts = new CancellationTokenSource();

		try
		{
			// Sort by latest update, only works if your target is top 30 that recently updated
			return await SendRequest<List<GitHubRepo>>($"{_url}/users/{user}/repos?per_page={pageSize}&sort=updated", _cts.Token);
		}
		catch
		{
			return null;
		}
	}

	public static async Task<QueryResult<List<GitHubRepo>, Exception>?> UserTokenQuery(int pageSize)
	{
		_cts?.Cancel();
		_cts = new CancellationTokenSource();

		try
		{
			return await SendRequest<List<GitHubRepo>>($"{_url}/user/repos?per_page={pageSize}&sort=updated", _cts.Token);
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
			HttpResponseMessage responseMessage = await _client.GetAsync(url, token);
			_ = responseMessage.EnsureSuccessStatusCode();
			var json = await responseMessage.Content.ReadAsStringAsync(token);
			T? response = JsonSerializer.Deserialize<T>(json);
			return response!;
		}
		catch (OperationCanceledException)
		{
			return new Exception("Request was cancelled.");
		}
		catch (Exception e)
		{
			Log.Error(e.Message, typeof(Main));
			return e;
		}
	}
}
