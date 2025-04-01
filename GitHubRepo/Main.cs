using Community.PowerToys.Run.Plugin.GitHubRepo.Properties;
using LazyCache;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Wox.Infrastructure;
using Wox.Plugin;
using BrowserInfo = Wox.Plugin.Common.DefaultBrowserInfo;

namespace Community.PowerToys.Run.Plugin.GitHubRepo;

public partial class Main : IPlugin, IPluginI18n, ISettingProvider, IReloadable, IDisposable, IDelayedExecutionPlugin, IContextMenu
{
	private static readonly CompositeFormat PluginInBrowserName = CompositeFormat.Parse(Resources.in_browser_name);
	private const string DefaultUser = nameof(DefaultUser);
	private const string AuthToken = nameof(AuthToken);
	private const string SelfHostUrl = nameof(SelfHostUrl);

	private string? _iconFolderPath;
	private string? _iconFork;
	private string? _iconRepo;
	private string? _icon;
	private string? _defaultUser;
	private string? _authToken;
	private CachingService? _cache;
	// additional data for context menu
	private record ResultData(string Url);

	private PluginInitContext? _context;
	private bool _disposed;
	public string Name => Resources.plugin_name;
	public string Description => Resources.plugin_description;
	public static string PluginID => "47B63DBFBDEE4F9C85EBA5F6CD69E243";

	public IEnumerable<PluginAdditionalOption> AdditionalOptions =>
	[
		new()
		{
			PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Textbox,
			Key = DefaultUser,
			DisplayLabel = Resources.option_default_user,
			DisplayDescription = Resources.option_default_user_desc,
			// Max length of a GitHub username is 39
			TextBoxMaxLength = 39,
		},
		new()
		{
			PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Textbox,
			Key = AuthToken,
			DisplayLabel = Resources.option_auth_token,
		},
		new()
		{
			PluginOptionType = PluginAdditionalOption.AdditionalOptionType.CheckboxAndTextbox,
			Key = SelfHostUrl,
			DisplayLabel = Resources.option_self_host_link,
			DisplayDescription = Resources.option_self_host_link_desc,
			SecondDisplayLabel = Resources.option_url,
		}
	];

	public void UpdateSettings(PowerLauncherPluginSettings settings)
	{
		_defaultUser = settings?.AdditionalOptions?.FirstOrDefault(static x => x.Key == DefaultUser)?.TextValue ?? string.Empty;
		// TODO: how to hide the auth token in settings?
		_authToken = settings?.AdditionalOptions?.FirstOrDefault(static x => x.Key == AuthToken)?.TextValue ?? string.Empty;
		GitHub.UpdateAuthSetting(_authToken);
		PluginAdditionalOption? selfHostUrl = settings?.AdditionalOptions?.FirstOrDefault(static x => x.Key == SelfHostUrl);
		GitHub.Url = selfHostUrl!.Value ? selfHostUrl.TextValue : string.Empty;
	}

	// handle user repo user
	public List<Result> Query(Query query)
	{
		ArgumentNullException.ThrowIfNull(query);

		var search = query.Search;

		// empty query
		if (string.IsNullOrEmpty(search))
		{
			var arguments = "github.com";
			return
			[
				new Result
				{
					Title = Resources.open_github,
					SubTitle = string.Format(CultureInfo.CurrentCulture, PluginInBrowserName, BrowserInfo.Name ?? BrowserInfo.MSEdgeName),
					QueryTextDisplay = string.Empty,
					IcoPath = _icon,
					ProgramArguments = arguments,
					Action = action => Helper.OpenCommandInShell(BrowserInfo.Path, BrowserInfo.ArgumentsPattern, arguments),
				}
			];
		}

		// delay execution for repo query
		if (!search.Contains('/'))
		{
			return [];
		}

		List<GitHubRepo> repos;
		string user;
		string target;

		if (search.StartsWith('/'))
		{
			if (string.IsNullOrEmpty(_defaultUser))
			{
				return
				[
					new Result
					{
						Title = Resources.default_user_not_set,
						SubTitle = Resources.default_user_not_set_description,
						QueryTextDisplay = string.Empty,
						IcoPath = _icon,
						Action = action => true,
					}
				];
			}

			user = _defaultUser;
			target = search[1..];
			repos = !string.IsNullOrEmpty(_authToken) ? _cache.GetOrAdd(user, UserTokenQuery) : _cache.GetOrAdd(user, () => UserRepoQuery(user));
		}
		else
		{
			var split = search.Split('/', 2);

			user = split[0];
			target = split[1];

			repos = _cache.GetOrAdd(user, () => UserRepoQuery(user));
		}

		List<Result> results = repos.ConvertAll(repo =>
		{
			var parts = repo.FullName.Split('/', 2);
			var repoName = parts.Length == 1 ? parts[0] : parts[1];
			MatchResult match = StringMatcher.FuzzySearch(target, repoName);
			return new Result
			{
				Title = repo.FullName,
				SubTitle = repo.Description,
				QueryTextDisplay = search,
				IcoPath = repo.Fork ? _iconFork : _iconRepo,
				Score = match.Score,
				TitleHighlightData = match.MatchData?.ConvertAll(e => e + user.Length + 1),
				ContextData = new ResultData(repo.HtmlUrl),
				Action = action => Helper.OpenCommandInShell(BrowserInfo.Path, BrowserInfo.ArgumentsPattern, repo.HtmlUrl),
			};
		});

		if (!string.IsNullOrEmpty(target))
		{
			_ = results.RemoveAll(r => r.Score <= 0);
		}

		return results;

		static List<GitHubRepo> UserRepoQuery(string user) => GitHub.UserRepoQuery(user).Result!.Match(
			ok: r => r,
			err: e => [new(e.GetType().Name, string.Empty, e.Message, false)]);

		static List<GitHubRepo> UserTokenQuery() => GitHub.UserTokenQuery().Result!.Match(
			ok: r => r,
			err: e => [new(e.GetType().Name, string.Empty, e.Message, false)]);
	}

	// handle repo search with delay
	public List<Result> Query(Query query, bool delayedExecution)
	{
		return !delayedExecution || query.Search.Contains('/') || string.IsNullOrWhiteSpace(query.Search)
			? []
			: RepoQuery(query.Search).ConvertAll(repo => new Result
			{
				Title = repo.FullName,
				SubTitle = repo.Description,
				QueryTextDisplay = query.Search,
				IcoPath = repo.Fork ? _iconFork : _iconRepo,
				ContextData = new ResultData(repo.HtmlUrl),
				Action = action => Helper.OpenCommandInShell(BrowserInfo.Path, BrowserInfo.ArgumentsPattern, repo.HtmlUrl),
			});

		static List<GitHubRepo> RepoQuery(string search) => GitHub.RepoQuery(search).Result.Match(
				ok: r => r.Items,
				err: e => [new(e.GetType().Name, string.Empty, e.Message, false)]);
	}

	public List<ContextMenuResult> LoadContextMenus(Result selectedResult)
	{
		if (selectedResult.ContextData is not ResultData selectedData)
		{
			return [];
		}

		var url = selectedData.Url;
		var issue = $"{url}/issues";
		var pr = $"{url}/pulls";
		return [
			new ()
			{
				PluginName = Name,
				Title = Resources.context_copy_link,
				Glyph = "\xE8C8",
				FontFamily = "Segoe Fluent Icons,Segoe MDL2 Assets",
				AcceleratorKey = Key.C,
				AcceleratorModifiers = ModifierKeys.Control,
				Action = _ =>
				{
					Clipboard.SetText(url);
					return true;
				},
			},
			new ()
			{
				PluginName = Name,
				Title = Resources.context_open_issues,
				Glyph = "\xE958",
				FontFamily = "Segoe Fluent Icons,Segoe MDL2 Assets",
				AcceleratorKey = Key.I,
				AcceleratorModifiers = ModifierKeys.Control,
				Action = _ => Helper.OpenCommandInShell(BrowserInfo.Path, BrowserInfo.ArgumentsPattern, issue),
			},
			new ()
			{
				PluginName = Name,
				Title = Resources.context_open_pull_requests,
				Glyph = "\xF003",
				FontFamily = "Segoe Fluent Icons,Segoe MDL2 Assets",
				AcceleratorKey = Key.P,
				AcceleratorModifiers = ModifierKeys.Control,
				Action = _ => Helper.OpenCommandInShell(BrowserInfo.Path, BrowserInfo.ArgumentsPattern, pr),
			},
		];
	}

	public void Init(PluginInitContext context)
	{
		_context = context ?? throw new ArgumentNullException(nameof(context));
		_context.API.ThemeChanged += OnThemeChanged;
		_cache = new CachingService();
		_cache.DefaultCachePolicy.DefaultCacheDurationSeconds = (int)TimeSpan.FromMinutes(1).TotalSeconds;
		UpdateIconPath(_context.API.GetCurrentTheme());
		BrowserInfo.UpdateIfTimePassed();
	}

	public string GetTranslatedPluginTitle() => Resources.plugin_name;

	public string GetTranslatedPluginDescription() => Resources.plugin_description;

	private void OnThemeChanged(Theme oldTheme, Theme newTheme) => UpdateIconPath(newTheme);

	private void UpdateIconPath(Theme theme)
	{
		_iconFolderPath = theme is Theme.Light or Theme.HighContrastWhite ? "Images\\light" : "Images\\dark";
		_icon = $"{_iconFolderPath}\\GitHub.png";
		_iconRepo = $"{_iconFolderPath}\\Repo.png";
		_iconFork = $"{_iconFolderPath}\\Fork.png";
	}

	public Control CreateSettingPanel() => throw new NotImplementedException();

	public void ReloadData()
	{
		if (_context is null)
		{
			return;
		}

		UpdateIconPath(_context.API.GetCurrentTheme());
		BrowserInfo.UpdateIfTimePassed();
	}

	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	protected virtual void Dispose(bool disposing)
	{
		if (!_disposed && disposing)
		{
			if (_context != null && _context.API != null)
			{
				_context.API.ThemeChanged -= OnThemeChanged;
			}

			_disposed = true;
		}
	}
}
