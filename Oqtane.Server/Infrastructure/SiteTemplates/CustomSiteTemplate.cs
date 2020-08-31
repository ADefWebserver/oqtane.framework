using Oqtane.Models;
using Oqtane.Infrastructure;
using System.Collections.Generic;
using Oqtane.Repository;
using Microsoft.AspNetCore.Hosting;
using Oqtane.Extensions;
using Oqtane.Shared;
using System.IO;

namespace Oqtane.SiteTemplates
{
    public class CustomSiteTemplate : ISiteTemplate
    {

        private readonly IWebHostEnvironment _environment;
        private readonly ISiteRepository _siteRepository;
        private readonly IFolderRepository _folderRepository;
        private readonly IFileRepository _fileRepository;

        public CustomSiteTemplate(IWebHostEnvironment environment, ISiteRepository siteRepository, IFolderRepository folderRepository, IFileRepository fileRepository)
        {
            _environment = environment;
            _siteRepository = siteRepository;
            _folderRepository = folderRepository;
            _fileRepository = fileRepository;
        }

        public string Name
        {
            get { return "Custom Site Template"; }
        }

        public List<PageTemplate> CreateSite(Site site)
        {
            List<PageTemplate> _pageTemplates = new List<PageTemplate>();

            _pageTemplates.Add(new PageTemplate
            {
                Name = "Home",
                Parent = "",
                Path = "",
                Icon = "home",
                IsNavigation = true,
                IsPersonalizable = false,
                PagePermissions = new List<Permission> {
                    new Permission(PermissionNames.View, Constants.AllUsersRole, true),
                    new Permission(PermissionNames.View, Constants.AdminRole, true),
                    new Permission(PermissionNames.Edit, Constants.AdminRole, true)
                }.EncodePermissions(),
                PageTemplateModules = new List<PageTemplateModule> {
                    new PageTemplateModule { ModuleDefinitionName = "Oqtane.Modules.HtmlText, Oqtane.Client", Title = "Welcome To Oqtane...", Pane = "Content",
                        ModulePermissions = new List<Permission> {
                            new Permission(PermissionNames.View, Constants.AllUsersRole, true),
                            new Permission(PermissionNames.View, Constants.AdminRole, true),
                            new Permission(PermissionNames.Edit, Constants.AdminRole, true)
                        }.EncodePermissions(),
                        Content = "<h4>This is my Custom Site Template</h4>"
                    }
                }
            });

            if (System.IO.File.Exists(Path.Combine(_environment.WebRootPath, "images", "logo-white.png")))
            {
                string folderpath = Utilities.PathCombine(_environment.ContentRootPath, "Content", "Tenants", site.TenantId.ToString(), "Sites", site.SiteId.ToString(), Path.DirectorySeparatorChar.ToString());
                System.IO.Directory.CreateDirectory(folderpath);
                if (!System.IO.File.Exists(Path.Combine(folderpath, "logo-white.png")))
                {
                    System.IO.File.Copy(Path.Combine(_environment.WebRootPath, "images", "logo-white.png"), Path.Combine(folderpath, "logo-white.png"));
                }
                Folder folder = _folderRepository.GetFolder(site.SiteId, "");
                Oqtane.Models.File file = _fileRepository.AddFile(new Oqtane.Models.File { FolderId = folder.FolderId, Name = "logo-white.png", Extension = "png", Size = 8192, ImageHeight = 80, ImageWidth = 250 });
                site.LogoFileId = file.FileId;
                _siteRepository.UpdateSite(site);
            }

            return _pageTemplates;
        }
    }
}
