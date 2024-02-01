using System.Globalization;
using System.Text;
using System.Windows.Controls;
using Community.PowerToys.Run.Plugin.GithubRepo.Properties;
using LazyCache;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Wox.Infrastructure;
using Wox.Plugin;
using Wox.Plugin.Logger;
using BrowserInfo = Wox.Plugin.Common.DefaultBrowserInfo;

namespace Community.PowerToys.Run.Plugin.GithubRepo
{
    public partial class Main : IPlugin, IPluginI18n, ISettingProvider, IReloadable, IDisposable, IDelayedExecutionPlugin
    {
        private static readonly CompositeFormat ErrorMsgFormat = CompositeFormat.Parse(Resources.plugin_search_failed);
        private static readonly CompositeFormat PluginInBrowserName = CompositeFormat.Parse(Resources.plugin_in_browser_name);
        private const string DefaultUser = nameof(DefaultUser);
        private const string AuthToken = nameof(AuthToken);

        private string? _iconFolderPath;
        private string? _iconFork;
        private string? _iconRepo;
        private string? _icon;
        private string? _defaultUser;
        private string? _authToken;

        private CachingService? _cache;

        // Should only be set in Init()
        private Action? onPluginError;


        private PluginInitContext? _context;

        private bool _disposed;

        public string Name => Resources.plugin_name;

        public string Description => Resources.plugin_description;

        public string EmptyDescription => Resources.plugin_empty_description;

        public static string PluginID => "47B63DBFBDEE4F9C85EBA5F6CD69E243";

        public IEnumerable<PluginAdditionalOption> AdditionalOptions => new List<PluginAdditionalOption>()
        {
            new()
            {
                PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Textbox,
                Key = DefaultUser,
                DisplayLabel = Resources.plugin_default_user,
                TextBoxMaxLength = 39,
                Value = false,
            },
            new()
            {
                PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Textbox,
                Key = AuthToken,
                DisplayLabel = Resources.plugin_auth_token,
                Value = false,
            },
        };

        // handle user repo search
        public List<Result> Query(Query query)
        {
            ArgumentNullException.ThrowIfNull(query);

            List<Result> results = [];
            string search = query.Search;
            List<GithubRepo>? repos;
            string target = string.Empty;

            // empty query
            if (string.IsNullOrEmpty(search))
            {
                string arguments = "github.com";
                results.Add(new Result
                {
                    Title = EmptyDescription,
                    SubTitle = string.Format(CultureInfo.CurrentCulture, PluginInBrowserName, BrowserInfo.Name ?? BrowserInfo.MSEdgeName),
                    QueryTextDisplay = string.Empty,
                    IcoPath = _icon,
                    ProgramArguments = arguments,
                    Action = action =>
                    {
                        if (!Helper.OpenCommandInShell(BrowserInfo.Path, BrowserInfo.ArgumentsPattern, arguments))
                        {
                            onPluginError!();
                            return false;
                        }

                        return true;
                    },
                });
                return results;
            }

            // delay execution for repo query
            if (!search.Contains('/'))
            {
                throw new OperationCanceledException();
            }

            if (search.StartsWith('/'))
            {
                if (string.IsNullOrEmpty(_defaultUser))
                {
                    results.Add(new Result
                    {
                        Title = Resources.plugin_default_user_not_set,
                        SubTitle = Resources.plugin_default_user_not_set_description,
                        QueryTextDisplay = string.Empty,
                        IcoPath = _icon,
                        Action = action =>
                        {
                            return true;
                        },
                    });
                    return results;
                }

                string cacheKey = _defaultUser;
                target = $"{_defaultUser}{search}";

                repos = _cache.GetOrAdd(cacheKey, () => UserRepoQuery(cacheKey));
            }
            else
            {
                string[] split = search.Split('/', 2);

                string cacheKey = split[0];
                target = search;

                repos = _cache.GetOrAdd(cacheKey, () => UserRepoQuery(cacheKey));
            }

            foreach (var repo in repos)
            {
                results.Add(new Result
                {
                    Title = repo.FullName,
                    SubTitle = repo.Description,
                    QueryTextDisplay = repo.FullName,
                    IcoPath = repo.Fork ? _iconFork : _iconRepo,
                    Action = action =>
                    {
                        if (!Helper.OpenCommandInShell(BrowserInfo.Path, BrowserInfo.ArgumentsPattern, repo.HtmlUrl))
                        {
                            onPluginError!();
                            return false;
                        }

                        return true;
                    },
                });
            }

            // TODO: other search algorithm
            results = results.Where(r => r.Title.StartsWith(target, StringComparison.OrdinalIgnoreCase)).ToList();
            return results;

            static List<GithubRepo> UserRepoQuery(string search) =>
                Github.UserRepoQuery(search).Result.Match(
                    ok: r => r,
                    err: e => new List<GithubRepo> { new(e.GetType().Name, string.Empty, e.Message, false) });
        }

        // handle repo search with delay
        public List<Result> Query(Query query, bool delayedExecution)
        {
            if (!delayedExecution || query.Search.Contains('/') || string.IsNullOrWhiteSpace(query.Search))
            {
                throw new OperationCanceledException();
            }

            List<Result> results = [];

            var repos = RepoQuery(query.Search);

            foreach (var repo in repos)
            {
                results.Add(new Result
                {
                    Title = repo.FullName,
                    SubTitle = repo.Description,
                    QueryTextDisplay = repo.FullName,
                    IcoPath = repo.Fork ? _iconFork : _iconRepo,
                    Action = action =>
                    {
                        if (!Helper.OpenCommandInShell(BrowserInfo.Path, BrowserInfo.ArgumentsPattern, repo.HtmlUrl))
                        {
                            onPluginError!();
                            return false;
                        }

                        return true;
                    },
                });
            }

            return results;

            static List<GithubRepo> RepoQuery(string search) =>
                Github.RepoQuery(search).Result.Match(
                    ok: r => r.Items,
                    err: e => new List<GithubRepo> { new(e.GetType().Name, string.Empty, e.Message, false) });
        }

        public void Init(PluginInitContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _context.API.ThemeChanged += OnThemeChanged;
            _cache = new CachingService();
            _cache.DefaultCachePolicy.DefaultCacheDurationSeconds = (int)TimeSpan.FromMinutes(1).TotalSeconds;
            UpdateIconPath(_context.API.GetCurrentTheme());
            BrowserInfo.UpdateIfTimePassed();

            onPluginError = () =>
            {
                string errorMsgString = string.Format(CultureInfo.CurrentCulture, ErrorMsgFormat, BrowserInfo.Name ?? BrowserInfo.MSEdgeName);

                Log.Error(errorMsgString, GetType());
                _context.API.ShowMsg(
                    $"Plugin: {Resources.plugin_name}",
                    errorMsgString);
            };
        }

        public string GetTranslatedPluginTitle()
        {
            return Resources.plugin_name;
        }

        public string GetTranslatedPluginDescription()
        {
            return Resources.plugin_description;
        }

        private void OnThemeChanged(Theme oldTheme, Theme newTheme)
        {
            UpdateIconPath(newTheme);
        }

        private void UpdateIconPath(Theme theme)
        {
            if (theme is Theme.Light or Theme.HighContrastWhite)
            {
                _iconFolderPath = "Images\\light";
            }
            else
            {
                _iconFolderPath = "Images\\dark";
            }
            _icon = $"{_iconFolderPath}\\Github.png";
            _iconRepo = $"{_iconFolderPath}\\Repo.png";
            _iconFork = $"{_iconFolderPath}\\Fork.png";
        }

        public Control CreateSettingPanel()
        {
            throw new NotImplementedException();
        }

        public void UpdateSettings(PowerLauncherPluginSettings settings)
        {
            _defaultUser = settings?.AdditionalOptions?.FirstOrDefault(x => x.Key == DefaultUser)?.TextValue ?? string.Empty;
            // TODO: how to hide the auth token in settings?
            _authToken = settings?.AdditionalOptions?.FirstOrDefault(x => x.Key == AuthToken)?.TextValue ?? string.Empty;
            Github.UpdateAuthSetting(_authToken);
        }

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
}
