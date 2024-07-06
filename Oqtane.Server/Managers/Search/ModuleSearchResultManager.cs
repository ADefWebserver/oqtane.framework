using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Oqtane.Models;
using Oqtane.Repository;
using Oqtane.Security;
using Oqtane.Services;
using Oqtane.Shared;

namespace Oqtane.Managers.Search
{
    public class ModuleSearchResultManager : ISearchResultManager
    {
        public string Name => EntityNames.Module;

        private readonly IServiceProvider _serviceProvider;

        public ModuleSearchResultManager(
            IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public string GetUrl(SearchResult searchResult, SearchQuery searchQuery)
        {
            var pageRepository = _serviceProvider.GetRequiredService<IPageRepository>();
            var pageIdValue = searchResult.SearchContentProperties?.FirstOrDefault(i => i.Name == Constants.SearchPageIdPropertyName)?.Value ?? string.Empty;
            if(!string.IsNullOrEmpty(pageIdValue) && int.TryParse(pageIdValue, out int pageId))
            {
                var page = pageRepository.GetPage(pageId);
                if (page != null)
                {
                    return $"{searchQuery.Alias.Protocol}{searchQuery.Alias.Name}{(!string.IsNullOrEmpty(page.Path) && !page.Path.StartsWith("/") ? "/" : "")}{page.Path}";
                }
            }
            
            return string.Empty;
        }

        public bool Visible(SearchContent searchResult, SearchQuery searchQuery)
        {
            var pageIdValue = searchResult.SearchContentProperties?.FirstOrDefault(i => i.Name == Constants.SearchPageIdPropertyName)?.Value ?? string.Empty;
            if (!string.IsNullOrEmpty(pageIdValue) && int.TryParse(pageIdValue, out int pageId))
            {
                return CanViewPage(pageId, searchQuery.User);
            }

            return false;
        }

        private bool CanViewPage(int pageId, User user)
        {
            var pageRepository = _serviceProvider.GetRequiredService<IPageRepository>();
            var page = pageRepository.GetPage(pageId);

            return page != null && !page.IsDeleted && UserSecurity.IsAuthorized(user, PermissionNames.View, page.PermissionList)
                    && (Utilities.IsPageModuleVisible(page.EffectiveDate, page.ExpiryDate) || UserSecurity.IsAuthorized(user, PermissionNames.Edit, page.PermissionList));
        }
    }
}
