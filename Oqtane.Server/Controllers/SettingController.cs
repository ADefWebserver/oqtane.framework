using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Oqtane.Models;
using Oqtane.Shared;
using Oqtane.Security;
using System.Linq;
using Oqtane.Enums;
using Oqtane.Infrastructure;
using Oqtane.Repository;
using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;

namespace Oqtane.Controllers
{
    [Route(ControllerRoutes.ApiRoute)]
    public class SettingController : Controller
    {
        private readonly ISettingRepository _settings;
        private readonly IPageModuleRepository _pageModules;
        private readonly IUserPermissions _userPermissions;
        private readonly ISyncManager _syncManager;
        private readonly IAliasAccessor _aliasAccessor;
        private readonly IOptionsMonitorCache<CookieAuthenticationOptions> _cookieCache;
        private readonly IOptionsMonitorCache<OpenIdConnectOptions> _oidcCache;
        private readonly IOptionsMonitorCache<OAuthOptions> _oauthCache;
        private readonly IOptionsMonitorCache<IdentityOptions> _identityCache;
        private readonly ILogManager _logger;
        private readonly Alias _alias;
        private readonly string _visitorCookie;

        public SettingController(ISettingRepository settings, IPageModuleRepository pageModules, IUserPermissions userPermissions, ITenantManager tenantManager, ISyncManager syncManager, IAliasAccessor aliasAccessor, IOptionsMonitorCache<CookieAuthenticationOptions> cookieCache, IOptionsMonitorCache<OpenIdConnectOptions> oidcCache, IOptionsMonitorCache<OAuthOptions> oauthCache, IOptionsMonitorCache<IdentityOptions> identityCache, ILogManager logger)
        {
            _settings = settings;
            _pageModules = pageModules;
            _userPermissions = userPermissions;
            _syncManager = syncManager;
            _aliasAccessor = aliasAccessor;
            _cookieCache = cookieCache;
            _oidcCache = oidcCache;
            _oauthCache = oauthCache;
            _identityCache = identityCache;
            _logger = logger;
            _alias = tenantManager.GetAlias();
            _visitorCookie = Constants.VisitorCookiePrefix + _alias.SiteId.ToString();
        }

        // GET: api/<controller>
        [HttpGet]
        public IEnumerable<Setting> Get(string entityName, int entityId)
        {
            List<Setting> settings = new List<Setting>();
            if (IsAuthorized(entityName, entityId, PermissionNames.View))
            {
                settings = _settings.GetSettings(entityName, entityId).ToList();
                if (FilterPrivate(entityName, entityId))
                {
                    settings = settings.Where(item => !item.IsPrivate).ToList();
                }
            }
            else
            {
                // suppress unauthorized visitor logging as it is usually caused by clients that do not support cookies or private browsing sessions
                if (entityName != EntityNames.Visitor) 
                {
                    _logger.Log(LogLevel.Error, this, LogFunction.Read, "User Not Authorized To Access Settings {EntityName} {EntityId}", entityName, entityId);
                    HttpContext.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                }
            }
            return settings;
        }

        // GET api/<controller>/5/xxx
        [HttpGet("{id}/{entityName}")]
        public Setting Get(int id, string entityName)
        {
            Setting setting = _settings.GetSetting(entityName, id);
            if (IsAuthorized(setting.EntityName, setting.EntityId, PermissionNames.View))
            {
                if (FilterPrivate(entityName, id) && setting.IsPrivate)
                {
                    setting = null;
                }
                return setting;
            }
            else
            {
                if (setting != null && entityName != EntityNames.Visitor)
                {
                    _logger.Log(LogLevel.Error, this, LogFunction.Read, "User Not Authorized To Access Setting {EntityName} {SettingId}", entityName, id);
                    HttpContext.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                }
                else
                {
                    HttpContext.Response.StatusCode = (int)HttpStatusCode.NotFound;
                }
                return null;
            }
        }

        // POST api/<controller>
        [HttpPost]
        public Setting Post([FromBody] Setting setting)
        {
            if (ModelState.IsValid && IsAuthorized(setting.EntityName, setting.EntityId, PermissionNames.Edit))
            {
                setting = _settings.AddSetting(setting);
                AddSyncEvent(setting.EntityName, setting.EntityId, setting.SettingId, SyncEventActions.Create);
                _logger.Log(LogLevel.Information, this, LogFunction.Create, "Setting Added {Setting}", setting);
            }
            else
            {
                if (setting.EntityName != EntityNames.Visitor)
                {
                    _logger.Log(LogLevel.Error, this, LogFunction.Create, "User Not Authorized To Add Setting {Setting}", setting);
                    HttpContext.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                }
                setting = null;
            }
            return setting;
        }

        // PUT api/<controller>/5
        [HttpPut("{id}")]
        public Setting Put(int id, [FromBody] Setting setting)
        {
            if (ModelState.IsValid && setting.SettingId == id && IsAuthorized(setting.EntityName, setting.EntityId, PermissionNames.Edit))
            {
                setting = _settings.UpdateSetting(setting);
                AddSyncEvent(setting.EntityName, setting.EntityId, setting.SettingId, SyncEventActions.Update);
                _logger.Log(LogLevel.Information, this, LogFunction.Update, "Setting Updated {Setting}", setting);
            }
            else
            {
                if (setting.EntityName != EntityNames.Visitor)
                {
                    _logger.Log(LogLevel.Error, this, LogFunction.Update, "User Not Authorized To Update Setting {Setting}", setting);
                    HttpContext.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                }
                setting = null;
            }
            return setting;
        }

        // PUT api/<controller>/site/1/settingname/x/false
        [HttpPut("{entityName}/{entityId}/{settingName}/{settingValue}/{isPrivate}")]
        public void Put(string entityName, int entityId, string settingName, string settingValue, bool isPrivate)
        {
            if (IsAuthorized(entityName, entityId, PermissionNames.Edit))
            {
                Setting setting = _settings.GetSetting(entityName, entityId, settingName);
                if (setting == null)
                {
                    setting = new Setting();
                    setting.EntityName = entityName;
                    setting.EntityId = entityId;
                    setting.SettingName = settingName;
                    setting.SettingValue = settingValue;
                    setting.IsPrivate = isPrivate;
                    setting = _settings.AddSetting(setting);
                    AddSyncEvent(setting.EntityName, setting.EntityId, setting.SettingId, SyncEventActions.Create);
                    _logger.Log(LogLevel.Information, this, LogFunction.Update, "Setting Created {Setting}", setting);
                }
                else
                {
                    if (setting.SettingValue != settingValue || setting.IsPrivate != isPrivate)
                    {
                        setting.SettingValue = settingValue;
                        setting.IsPrivate = isPrivate;
                        setting = _settings.UpdateSetting(setting);
                        AddSyncEvent(setting.EntityName, setting.EntityId, setting.SettingId, SyncEventActions.Update);
                        _logger.Log(LogLevel.Information, this, LogFunction.Update, "Setting Updated {Setting}", setting);
                    }
                }
            }
            else
            {
                _logger.Log(LogLevel.Error, this, LogFunction.Update, "User Not Authorized To Add Or Update Setting {EntityName} {EntityId} {SettingName}", entityName, entityId, settingName);
                HttpContext.Response.StatusCode = (int)HttpStatusCode.Forbidden;
            }
        }

        // DELETE api/<controller>/site/1/settingname
        [HttpDelete("{entityName}/{entityId}/{settingName}")]
        public void Delete(string entityName, int entityId, string settingName)
        {
            Setting setting = _settings.GetSetting(entityName, entityId, settingName);
            if (setting != null && IsAuthorized(setting.EntityName, setting.EntityId, PermissionNames.Edit))
            {
                _settings.DeleteSetting(setting.EntityName, setting.SettingId);
                AddSyncEvent(setting.EntityName, setting.EntityId, setting.SettingId, SyncEventActions.Delete);
                _logger.Log(LogLevel.Information, this, LogFunction.Delete, "Setting Deleted {Setting}", setting);
            }
            else
            {
                if (entityName != EntityNames.Visitor)
                {
                    _logger.Log(LogLevel.Error, this, LogFunction.Delete, "Setting Does Not Exist Or User Not Authorized To Delete Setting For Entity {EntityName} Id {EntityId} Name {SettingName}", entityName, entityId, settingName);
                    HttpContext.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                }
            }
        }

        // DELETE api/<controller>/clear
        [HttpDelete("clear")]
        [Authorize(Roles = RoleNames.Admin)]
        public void Clear()
        {
            // clear SiteOptionsCache for each option type
            var cookieCache = new SiteOptionsCache<CookieAuthenticationOptions>(_aliasAccessor);
            cookieCache.Clear();
            var oidcCache = new SiteOptionsCache<OpenIdConnectOptions>(_aliasAccessor);
            oidcCache.Clear();
            var oauthCache = new SiteOptionsCache<OAuthOptions>(_aliasAccessor);
            oauthCache.Clear();
            var identityCache = new SiteOptionsCache<IdentityOptions>(_aliasAccessor);
            identityCache.Clear();

            // clear IOptionsMonitorCache for each option type
            _cookieCache.Clear();
            _oidcCache.Clear();
            _oauthCache.Clear();
            _identityCache.Clear();

            _logger.Log(LogLevel.Information, this, LogFunction.Other, "Site Options Cache Cleared");
        }

        private bool IsAuthorized(string entityName, int entityId, string permissionName)
        {
            bool authorized = false;
            if (entityName == EntityNames.PageModule)
            {
                entityName = EntityNames.Module;
                entityId = _pageModules.GetPageModule(entityId).ModuleId;
            }
            switch (entityName)
            {
                case EntityNames.Tenant:
                case EntityNames.ModuleDefinition:
                case EntityNames.Host:
                case EntityNames.Job:
                case EntityNames.Theme:
                    if (permissionName == PermissionNames.Edit)
                    {
                        authorized = User.IsInRole(RoleNames.Host);
                    }
                    else
                    {
                        authorized = true;
                    }
                    break;
                case EntityNames.Site:
                    if (permissionName == PermissionNames.Edit)
                    {
                        authorized = User.IsInRole(RoleNames.Admin);
                    }
                    else
                    {
                        authorized = true;
                    }
                    break;
                case EntityNames.Page:
                case EntityNames.Module:
                case EntityNames.Folder:
                    authorized = _userPermissions.IsAuthorized(User, _alias.SiteId, entityName, entityId, permissionName);
                    break;
                case EntityNames.User:
                    authorized = _userPermissions.IsAuthorized(User, _alias.SiteId, entityName, -1, PermissionNames.Write, RoleNames.Admin) || (_userPermissions.GetUser(User).UserId == entityId);
                    break;
                case EntityNames.Visitor:
                    authorized = User.IsInRole(RoleNames.Admin);
                    if (!authorized)
                    {
                        // a visitor may have cookies disabled
                        if (int.TryParse(Request.Cookies[_visitorCookie], out int visitorId))
                        {
                            authorized = (visitorId == entityId);
                        }
                    }
                    break;
                default: // custom entity
                    authorized = true;
                    if (permissionName == PermissionNames.Edit)
                    {
                        authorized = _userPermissions.IsAuthorized(User, _alias.SiteId, entityName, entityId, permissionName) ||
                            _userPermissions.IsAuthorized(User, _alias.SiteId, entityName, -1, PermissionNames.Write, RoleNames.Admin);
                    }
                    break;
            }
            return authorized;
        }

        private bool FilterPrivate(string entityName, int entityId)
        {
            bool filter = false;
            switch (entityName)
            {
                case EntityNames.Tenant:
                case EntityNames.ModuleDefinition:
                case EntityNames.Host:
                case EntityNames.Job:
                case EntityNames.Theme:
                    filter = !User.IsInRole(RoleNames.Host);
                    break;
                case EntityNames.Site:
                    filter = !User.IsInRole(RoleNames.Admin);
                    break;
                case EntityNames.Page:
                case EntityNames.Module:
                case EntityNames.Folder:
                    filter = !_userPermissions.IsAuthorized(User, _alias.SiteId, entityName, entityId, PermissionNames.Edit);
                    break;
                case EntityNames.User:
                    filter = !_userPermissions.IsAuthorized(User, _alias.SiteId, entityName, -1, PermissionNames.Write, RoleNames.Admin) && _userPermissions.GetUser(User).UserId != entityId;
                    break;
                case EntityNames.Visitor:
                    if (!User.IsInRole(RoleNames.Admin))
                    {
                        filter = true;
                        if (int.TryParse(Request.Cookies[_visitorCookie], out int visitorId))
                        {
                            filter = (visitorId != entityId);
                        }
                    }
                    break;
                default: // custom entity
                    filter = !User.IsInRole(RoleNames.Admin) && !_userPermissions.IsAuthorized(User, _alias.SiteId, entityName, entityId, PermissionNames.Edit);
                    break;
            }
            return filter;
        }

        private void AddSyncEvent(string EntityName, int EntityId, int SettingId, string Action)
        {
            _syncManager.AddSyncEvent(_alias, EntityName + "Setting", SettingId, Action);

            switch (EntityName)
            {
                case EntityNames.Module:
                case EntityNames.Page:
                case EntityNames.Site:
                    _syncManager.AddSyncEvent(_alias, EntityNames.Site, _alias.SiteId, SyncEventActions.Refresh);
                    break;
                case EntityNames.User:
                    _syncManager.AddSyncEvent(_alias, EntityName, EntityId, SyncEventActions.Update);
                    break;
            }
        }
    }
}
