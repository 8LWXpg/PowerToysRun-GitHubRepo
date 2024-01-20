// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Caching;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Wox.Infrastructure;
using Wox.Plugin;
using Wox.Plugin.Logger;
using BrowserInfo = Wox.Plugin.Common.DefaultBrowserInfo;

namespace Community.PowerToys.Run.Plugin.GithubRepo
{
    public partial class Main : IPlugin, IPluginI18n, ISettingProvider, IReloadable, IDisposable
    {
        private static readonly CompositeFormat ErrorMsgFormat = CompositeFormat.Parse(Properties.Resources.plugin_search_failed);
        private static readonly CompositeFormat PluginInBrowserName = CompositeFormat.Parse(Properties.Resources.plugin_in_browser_name);
        private const string IconFork = "Fork.png";
        private const string IconRepo = "Repo.png";
        private const string DefaultUser = nameof(DefaultUser);
        private const string AuthToken = nameof(AuthToken);

        private string _iconFolder;
        private string _defaultUser;
        private string _authToken;

        private MemoryCache _cache = MemoryCache.Default;

        // Should only be set in Init()
        private Action onPluginError;

        private PluginInitContext _context;

        private string _iconPath;

        private bool _disposed;

        public string Name => Properties.Resources.plugin_name;

        public string Description => Properties.Resources.plugin_description;

        public string EmptyDescription => Properties.Resources.plugin_empty_description;

        public static string PluginID => "47B63DBFBDEE4F9C85EBA5F6CD69E243";

        public IEnumerable<PluginAdditionalOption> AdditionalOptions => new List<PluginAdditionalOption>()
        {
            new PluginAdditionalOption()
            {
                PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Textbox,
                Key = DefaultUser,
                DisplayLabel = Properties.Resources.plugin_default_user,
                TextValue = string.Empty,
            },
            new PluginAdditionalOption()
            {
                PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Textbox,
                Key = AuthToken,
                DisplayLabel = Properties.Resources.plugin_auth_token,
                TextValue = string.Empty,
            },
        };

        private struct GithubResponse
        {
            [JsonPropertyName("items")]
            public List<GithubRepo> Items { get; set; }
        }

        private struct GithubRepo
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

        private static class Github
        {
            private static HttpClient client;

            // Used to cancel the request if the user types a new query
            private static CancellationTokenSource cts;

            static Github()
            {
                client = new HttpClient();
                client.DefaultRequestHeaders.UserAgent.Add(ProductInfoHeaderValue.Parse("PowerToys"));
                client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
                client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
            }

            public static void UpdateAuthSetting(string auth)
            {
                if (string.IsNullOrEmpty(auth))
                {
                    client.DefaultRequestHeaders.Remove("Authorization");
                }
                else
                {
                    client.DefaultRequestHeaders.Add("Authorization:", $"Bearer {auth}");
                }
            }

            public static async Task<List<GithubRepo>> RepoQuery(string query)
            {
                cts?.Cancel();
                cts = new CancellationTokenSource();

                var response = await SendRequest<GithubResponse>($"https://api.github.com/search/repositories?q={query}", cts.Token);
                return response.Items;
            }

            public static async Task<List<GithubRepo>> UserRepoQuery(string user)
            {
                cts?.Cancel();
                cts = new CancellationTokenSource();

                // assuming you're searching recent ones
                // TODO: cache this, determin update interval
                var response = await SendRequest<List<GithubRepo>>($"https://api.github.com/users/{user}/repos?sort=updated", cts.Token);
                return response;
            }

            private static async Task<T> SendRequest<T>(string url, CancellationToken token)
            {
                try
                {
                    var responseMessage = await client.GetAsync(url, token);
                    responseMessage.EnsureSuccessStatusCode();
                    var json = await responseMessage.Content.ReadAsStringAsync(token);
                    var response = JsonSerializer.Deserialize<T>(json);
                    return response;
                }
                catch (HttpRequestException e)
                {
                    Log.Error(e.Message, typeof(Main));
                    return default;
                }
            }
        }

        public List<Result> Query(Query query)
        {
            ArgumentNullException.ThrowIfNull(query);

            var results = new List<Result>();
            var search = query.Search;
            List<GithubRepo> repos;
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
                    IcoPath = _iconPath,
                    ProgramArguments = arguments,
                    Action = action =>
                    {
                        if (!Helper.OpenCommandInShell(BrowserInfo.Path, BrowserInfo.ArgumentsPattern, arguments))
                        {
                            onPluginError();
                            return false;
                        }

                        return true;
                    },
                });
                return results;
            }

            if (search.StartsWith('/'))
            {
                if (string.IsNullOrEmpty(_defaultUser))
                {
                    results.Add(new Result
                    {
                        Title = Properties.Resources.plugin_default_user_not_set,
                        SubTitle = Properties.Resources.plugin_default_user_not_set_description,
                        QueryTextDisplay = string.Empty,
                        IcoPath = _iconPath,
                        Action = action =>
                        {
                            return true;
                        },
                    });
                    return results;
                }

                var cacheKey = _defaultUser;
                target = $"{_defaultUser}{search}";
                repos = _cache[cacheKey] as List<GithubRepo>;

                if (repos is null)
                {
                    repos = Github.UserRepoQuery(cacheKey).Result;
                    _cache.Set(cacheKey, repos, new CacheItemPolicy { SlidingExpiration = TimeSpan.FromMinutes(1) });
                }
            }
            else if (search.Contains('/'))
            {
                var split = search.Split('/', 2);

                var cacheKey = split[0];
                target = search;
                repos = _cache[cacheKey] as List<GithubRepo>;

                if (repos is null)
                {
                    repos = Github.UserRepoQuery(cacheKey).Result;
                    _cache.Set(cacheKey, repos, new CacheItemPolicy { SlidingExpiration = TimeSpan.FromMinutes(1) });
                }
            }
            else
            {
                repos = Github.RepoQuery(search).Result;
            }

            foreach (var repo in repos)
            {
                results.Add(new Result
                {
                    Title = repo.FullName,
                    SubTitle = repo.Description,
                    QueryTextDisplay = repo.FullName,
                    IcoPath = repo.Fork ? Path.Combine(_iconFolder, IconFork) : Path.Combine(_iconFolder, IconRepo),
                    Action = action =>
                    {
                        if (!Helper.OpenCommandInShell(BrowserInfo.Path, BrowserInfo.ArgumentsPattern, repo.HtmlUrl))
                        {
                            onPluginError();
                            return false;
                        }

                        return true;
                    },
                });
            }

            // no need to fuzzy search if use repo search directly
            if (string.IsNullOrEmpty(target))
            {
                return results;
            }

            results = results.Where(r => r.Title.StartsWith(target, StringComparison.OrdinalIgnoreCase)).ToList();
            return results;
        }

        public void Init(PluginInitContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _context.API.ThemeChanged += OnThemeChanged;
            UpdateIconPath(_context.API.GetCurrentTheme());
            BrowserInfo.UpdateIfTimePassed();

            onPluginError = () =>
            {
                string errorMsgString = string.Format(CultureInfo.CurrentCulture, ErrorMsgFormat, BrowserInfo.Name ?? BrowserInfo.MSEdgeName);

                Log.Error(errorMsgString, GetType());
                _context.API.ShowMsg(
                    $"Plugin: {Properties.Resources.plugin_name}",
                    errorMsgString);
            };
        }

        public string GetTranslatedPluginTitle()
        {
            return Properties.Resources.plugin_name;
        }

        public string GetTranslatedPluginDescription()
        {
            return Properties.Resources.plugin_description;
        }

        private void OnThemeChanged(Theme oldtheme, Theme newTheme)
        {
            UpdateIconPath(newTheme);
        }

        private void UpdateIconPath(Theme theme)
        {
            if (theme == Theme.Light || theme == Theme.HighContrastWhite)
            {
                _iconPath = "Images/light/GithubRepo.png";
                _iconFolder = "Images/light";
            }
            else
            {
                _iconPath = "Images/dark/GithubRepo.png";
                _iconFolder = "Images/dark";
            }
        }

        public Control CreateSettingPanel()
        {
            throw new NotImplementedException();
        }

        public void UpdateSettings(PowerLauncherPluginSettings settings)
        {
            _defaultUser = settings?.AdditionalOptions?.FirstOrDefault(x => x.Key == DefaultUser)?.TextValue ?? string.Empty;
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

        [GeneratedRegex("[^\\u0020-\\u007E]")]
        private static partial Regex AllowedCharacters();
    }
}
