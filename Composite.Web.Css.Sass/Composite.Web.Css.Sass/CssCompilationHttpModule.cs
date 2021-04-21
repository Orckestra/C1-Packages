using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Threading;
using System.Web;
using Composite.C1Console.Security;
using Composite.Core.Collections.Generic;

namespace Orckestra.Web.Css.Sass
{
    public class CssCompilationHttpModule : IHttpModule
    {
        private static readonly ReaderWriterLockSlim _compilationLock = new ReaderWriterLockSlim();
        private static readonly Hashtable<string, CachedDirectoryInfo> _directoryInfo = new Hashtable<string, CachedDirectoryInfo>();

        private readonly string _extension;
        private readonly string _fileWatcherMask;
        private readonly Action<string, string, DateTime?> _compileAction;

        public CssCompilationHttpModule(string extension, string fileWatcherMask, Action<string, string, DateTime?> compileAction)
        {
            _extension = extension;
            _fileWatcherMask = fileWatcherMask;
            _compileAction = compileAction;
        }

        public void Init(HttpApplication application)
        {
            bool packageEnabled = ConfigurationManager.AppSettings["Orckestra.Web.Css.Sass"] == "true";
            if (!packageEnabled)
            {
                return;
            }
            application.BeginRequest += (a, b) => context_BeginRequest(application.Context);
        }

        private void context_BeginRequest(HttpContext context)
        {
            var requestPath = context.Request.Path;
            if (!requestPath.EndsWith(_extension))
            {
                return;
            }

            var filePath = context.Server.MapPath(requestPath);
            if (!File.Exists(filePath))
            {
                return;
            }

            string directory = Path.GetDirectoryName(filePath);

            DateTime folderLastUpdatedUtc = GetCachedFolderLastUpdateDateUtc(directory, _fileWatcherMask);

            var filePathCss = filePath.Substring(0, filePath.Length - _extension.Length) + ".min.css";

            if (!File.Exists(filePathCss) || File.GetLastWriteTimeUtc(filePathCss) < folderLastUpdatedUtc)
            {
                try
                {
                    _compilationLock.EnterWriteLock();

                    try
                    {
                        if (!File.Exists(filePathCss) || File.GetLastWriteTimeUtc(filePathCss) < folderLastUpdatedUtc)
                        {
                            _compileAction(filePath, filePathCss, folderLastUpdatedUtc);
                        }
                    }
                    catch (CssCompileException ex)
                    {
                        if (!UserValidationFacade.IsLoggedIn())
                        {
                            throw;
                        }

                        // Showing a friendly error message for logged in users
                        context.Response.Write(string.Format(@"
/* 
CSS COMPILE ERROR:
{2}
*/
body:before {{
   position: fixed;
   top: 10px;
   display: block;
   width: 60%;
   margin-left: 19%;
   padding: 20px;
   border: 2px solid red;
   font-family: 'Helvetica Neue',Helvetica,Arial,sans-serif;
   font-size: 12px;
   font-weight: bold;
   color: InfoText;
   white-space:pre;
   background-color: InfoBackground;
   content: 'Error in {0}:\A\A {1}';
   z-index: 10000;
}}", requestPath, EncodeCssContent(ex.Message), EncodeCssComment(ex.Message)));
                        context.ApplicationInstance.CompleteRequest();
                        return;
                    }
                }
                finally
                {
                    _compilationLock.ExitWriteLock();
                }
            }

            if ((DateTime.UtcNow - folderLastUpdatedUtc).Days >= 1 || !UserValidationFacade.IsLoggedIn())
            {
                context.Response.Cache.SetExpires(DateTime.Now.AddDays(1.0));
            }

            context.Response.ContentType = "text/css";

            try
            {
                _compilationLock.EnterReadLock();

                context.Response.WriteFile(filePathCss);
            }
            finally
            {
                _compilationLock.ExitReadLock();
            }

            context.ApplicationInstance.CompleteRequest();
        }

        private string EncodeCssContent(string text)
        {
            return text.Replace("'", "\"").Replace(@"\", @"\\").Replace("\n", @"\A ");
        }

        private string EncodeCssComment(string text)
        {
            return text.Replace("*", "");
        }

        private DateTime GetCachedFolderLastUpdateDateUtc(string directory, string fileMask)
        {
            var cacheRecord = _directoryInfo[directory];

            if (cacheRecord == null)
            {
                lock (_directoryInfo)
                {
                    cacheRecord = _directoryInfo[directory];
                    if (cacheRecord == null)
                    {
                        cacheRecord = new CachedDirectoryInfo();

                        cacheRecord.FileSystemWatcher = new FileSystemWatcher(directory, fileMask);
                        cacheRecord.FileSystemWatcher.IncludeSubdirectories = true;
                        cacheRecord.FileSystemWatcher.Created += (a, b) => cacheRecord.LastTimeUpdatedUtc = null;
                        cacheRecord.FileSystemWatcher.Changed += (a, b) => cacheRecord.LastTimeUpdatedUtc = null;
                        cacheRecord.FileSystemWatcher.Deleted += (a, b) => cacheRecord.LastTimeUpdatedUtc = null;
                        cacheRecord.FileSystemWatcher.Renamed += (a, b) => cacheRecord.LastTimeUpdatedUtc = null;

                        cacheRecord.FileSystemWatcher.EnableRaisingEvents = true;

                        _directoryInfo[directory] = cacheRecord;
                    }
                }
            }

            var cachedResult = cacheRecord.LastTimeUpdatedUtc;

            if (cachedResult.HasValue) return cachedResult.Value;

            var files = Directory.GetFiles(directory, fileMask, SearchOption.AllDirectories);

            if (files.Length == 0) return DateTime.Now;

            DateTime result = File.GetLastWriteTimeUtc(files[0]);
            foreach (var file in files.Skip(1))
            {
                DateTime lastUpdatedUtc = File.GetLastWriteTimeUtc(file);
                if (lastUpdatedUtc > result)
                {
                    result = lastUpdatedUtc;
                }
            }

            cacheRecord.LastTimeUpdatedUtc = result;

            return result;
        }

        public void Dispose()
        {
        }

        private class CachedDirectoryInfo
        {
            public FileSystemWatcher FileSystemWatcher { get; set; }
            public DateTime? LastTimeUpdatedUtc { get; set; }
        }
    }

}
