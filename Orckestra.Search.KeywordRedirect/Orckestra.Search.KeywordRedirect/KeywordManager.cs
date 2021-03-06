﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Composite.Core.Extensions;
using Composite.Core.Logging;
using Composite.Data;
using Composite.Data.ProcessControlled.ProcessControllers.GenericPublishProcessController;
using Composite.Data.PublishScheduling;
using Orckestra.Search.KeywordRedirect.Data.Types;
using Orckestra.Search.KeywordRedirect.Endpoint;
using System.Collections.Concurrent;
using Composite.Core.Routing;
using Composite.Core.WebClient.Renderings.Page;
using KeywordAndUrlDictionary = System.Collections.Generic.Dictionary<string, string>;
using HomepageDictionary = System.Collections.Generic.Dictionary<System.Guid, System.Collections.Generic.Dictionary<string, string>>;

namespace Orckestra.Search.KeywordRedirect
{
    public class KeywordManager : IObserver<KeywordChange>, IObserver<PageChange>, IObserver<RedirectKeyword>, IDisposable
    {
        public KeywordManager(KeywordChangeNotifier keywordChangeNotifier, PageChangeNotifier pageChangeNotifier, 
            BeforeKeywordChangeNotifier beforeKeywordChangeNotifier, ILog log)
        {
            _log = log;
            _keywordChangeNotifierUnsubscriber = keywordChangeNotifier.Subscribe(this);
            _pageChangeNotifierUnsubscriber = pageChangeNotifier.Subscribe(this);
            _beforeKeywordChangeNotifierUnsubscriber = beforeKeywordChangeNotifier.Subscribe(this);

            // NOTE: should be executed once at startup to fixed already installed packages
            FixMissingHomePages();
        }

        private readonly ILog _log;
        private readonly IDisposable _keywordChangeNotifierUnsubscriber;
        private readonly IDisposable _pageChangeNotifierUnsubscriber;
        private readonly IDisposable _beforeKeywordChangeNotifierUnsubscriber;

        private readonly ConcurrentDictionary<CultureInfo, Lazy<List<Model.RedirectKeyword>>> _keywordsCache = new ConcurrentDictionary<CultureInfo, Lazy<List<Model.RedirectKeyword>>>();
        private readonly ConcurrentDictionary<CultureInfo, Lazy<HomepageDictionary>> _keywordRedirectCache = new ConcurrentDictionary<CultureInfo, Lazy<HomepageDictionary>>();
        private readonly ConcurrentDictionary<Guid, Lazy<Guid>> _pageIdToHomePageIdCache = new ConcurrentDictionary<Guid, Lazy<Guid>>();
        private readonly ConcurrentDictionary<Guid, Lazy<string>> _homePageIdToTitleCache = new ConcurrentDictionary<Guid, Lazy<string>>();

        public void OnCompleted()
        {
            _log.LogWarning(nameof(KeywordManager), "Unexpected KeywordChange OnCompleted called");
        }

        public void OnError(Exception error)
        {
            _log.LogError(nameof(KeywordManager), error);
        }

        void IObserver<KeywordChange>.OnNext(KeywordChange value)
        {
            _keywordsCache.TryRemove(value.CultureInfo, out _);
            _keywordRedirectCache.TryRemove(value.CultureInfo, out _);
        }

        void IObserver<PageChange>.OnNext(PageChange value)
        {
            _keywordsCache.TryRemove(value.CultureInfo, out _);
            _keywordRedirectCache.TryRemove(value.CultureInfo, out _);
            _pageIdToHomePageIdCache.Clear();
            _homePageIdToTitleCache.Clear();
        }

        void IObserver<RedirectKeyword>.OnNext(RedirectKeyword redirectKeyword)
        {
            redirectKeyword.HomePage = GetHomePageIdByPageId(redirectKeyword.LandingPage);
        }

        public IEnumerable<Model.RedirectKeyword> GetKeywordRedirects(CultureInfo cultureInfo)
        {
            return _keywordsCache.GetOrAdd(cultureInfo, () => LoadKeywords(cultureInfo));
        }

        public string GetPublicRedirect(string keyword, CultureInfo cultureInfo, Guid currentHomePageId)
        {
            if (keyword == null)
                return null;

            var keywordsBySite = _keywordRedirectCache.GetOrAdd(cultureInfo, () => LoadKeywordRedirects(cultureInfo));
            if (!keywordsBySite.TryGetValue(currentHomePageId, out var redirectsByKeywords))
                return null;

            redirectsByKeywords.TryGetValue(keyword, out var url);
            return url;
        }

        private List<Model.RedirectKeyword> LoadKeywords(CultureInfo cultureInfo)
        {
            var result = new List<Model.RedirectKeyword>();
            using (var connection = new DataConnection(PublicationScope.Unpublished, cultureInfo))
            {
                foreach (var redirectKeyword in connection.Get<RedirectKeyword>())
                {
                    var interfaceType = redirectKeyword.DataSourceId.InterfaceType;
                    var stringKey = redirectKeyword.GetUniqueKey().ToString();
                    var locale = redirectKeyword.DataSourceId.LocaleScope.Name;

                    var existingPublishSchedule = PublishScheduleHelper.GetPublishSchedule(interfaceType, stringKey, locale);
                    var existingUnpublishSchedule = PublishScheduleHelper.GetUnpublishSchedule(interfaceType, stringKey, locale);

                    Model.RedirectKeyword keyword;
                    if (redirectKeyword.PublicationStatus == GenericPublishProcessController.Published)
                    {
                        keyword = new Model.RedirectKeyword
                        {
                            Keyword = redirectKeyword.Keyword,
                            LandingPage = KeywordFacade.GetPageUrl(redirectKeyword.LandingPage, cultureInfo),
                            PublishDate = existingPublishSchedule?.PublishDate.ToTimeZoneDateTimeString(),
                            UnpublishDate = existingUnpublishSchedule?.UnpublishDate.ToTimeZoneDateTimeString(),
                            HomePage = GetHomePageTitle(redirectKeyword.HomePage.GetValueOrDefault()),
                            HomePageId = redirectKeyword.HomePage.GetValueOrDefault(),
                        };
                    }
                    else
                    {
                        var publishedredirectKeyword = DataFacade.GetDataFromOtherScope(redirectKeyword, DataScopeIdentifier.Public).FirstOrDefault();
                        keyword = new Model.RedirectKeyword
                        {
                            Keyword = redirectKeyword.Keyword ?? publishedredirectKeyword?.Keyword,
                            LandingPage = publishedredirectKeyword != null ? KeywordFacade.GetPageUrl(publishedredirectKeyword.LandingPage, cultureInfo) : null,
                            LandingPageUnpublished = KeywordFacade.GetPageUrl(redirectKeyword.LandingPage, cultureInfo),
                            PublishDate = existingPublishSchedule?.PublishDate.ToTimeZoneDateTimeString(),
                            UnpublishDate = existingUnpublishSchedule?.UnpublishDate.ToTimeZoneDateTimeString(),
                            HomePage = GetHomePageTitle((redirectKeyword.HomePage ?? publishedredirectKeyword?.HomePage).GetValueOrDefault()),
                            HomePageId = (redirectKeyword.HomePage ?? publishedredirectKeyword?.HomePage).GetValueOrDefault(),
                        };
                    }

                    result.Add(keyword);
                }
            }

            return result;
        }

        private Guid GetHomePageIdByPageId(Guid pageId)
        {
            return _pageIdToHomePageIdCache.GetOrAdd(pageId, GetHomePageId);

            Guid GetHomePageId() => PageStructureInfo.GetAssociatedPageIds(pageId, SitemapScope.AncestorsAndCurrent).LastOrDefault();
        }

        private HomepageDictionary LoadKeywordRedirects(CultureInfo cultureInfo)
        {
            using (var connection = new DataConnection(PublicationScope.Published, cultureInfo))
            {
                var allRedirectKeywords = connection.Get<RedirectKeyword>().ToList();
                var result = allRedirectKeywords
                    .GroupBy(k => k.HomePage.GetValueOrDefault())
                    .ToDictionary(x => x.Key, GetPageUrlByKeyword);

                return result;

                KeywordAndUrlDictionary GetPageUrlByKeyword(IEnumerable<RedirectKeyword> redirectKeywords)
                {
                    return redirectKeywords
                        .GroupBy(x => x.Keyword, StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(x => x.Key, x => GetPageUrl(x.First()), StringComparer.OrdinalIgnoreCase);
                }

                string GetPageUrl(RedirectKeyword redirectKeyword)
                {
                    return PageUrls.BuildUrl(new PageUrlData(redirectKeyword.LandingPage, PublicationScope.Published, cultureInfo));
                }
            }
        }

        private void FixMissingHomePages()
        {
            foreach (var cultureInfo in DataLocalizationFacade.ActiveLocalizationCultures)
            {
                using (var connection = new DataConnection(PublicationScope.Unpublished, cultureInfo))
                {
                    var keywords = connection
                        .Get<RedirectKeyword>()
                        .Where(k => k.HomePage == null)
                        .ToList();

                    foreach (var keyword in keywords)
                    {
                        keyword.HomePage = GetHomePageIdByPageId(keyword.LandingPage);
                    }

                    connection.Update(keywords);
                }
            }
        }

        private string GetHomePageTitle(Guid homePageId)
        {
            return _homePageIdToTitleCache.GetOrAdd(homePageId, GetLabel);

            string GetLabel() => PageManager.GetPageById(homePageId)?.GetLabel();
        }

        public void Dispose()
        {
            _keywordChangeNotifierUnsubscriber.Dispose();
            _beforeKeywordChangeNotifierUnsubscriber.Dispose();
            _pageChangeNotifierUnsubscriber.Dispose();
        }
    };
}
