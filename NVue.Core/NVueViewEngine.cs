using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewEngines;

namespace NVue.Core{
    public class NVueViewEngine : IViewEngine {

        private string[] _viewLocationFormats;
        public NVueViewEngine(){
            _viewLocationFormats = new string[]{"Views/{1}/{0}.nvue", "Views/Shared/{0}.nvue"};
        }

        public ViewEngineResult FindView(ActionContext context, string viewName, bool isMainPage)
        {
            var controllerName = GetNormalizedRouteValue(context, "controller");

            var checkedLocations = new List<string>();
            foreach(var locationFormat in _viewLocationFormats){
                var location = string.Format(locationFormat, viewName, controllerName);
                
                if(File.Exists(location)){
                    return ViewEngineResult.Found("Default", new NVue(location, _viewLocationFormats, controllerName));
                }
                checkedLocations.Add(location);
            }
            return ViewEngineResult.NotFound(viewName, checkedLocations);
        }

        public ViewEngineResult GetView(string executingFilePath, string viewPath, bool isMainPage)
        {
            var appRelativePath = PathHelper.GetAbsolutePath(executingFilePath, viewPath);

            if(!PathHelper.IsAbsolutePath(viewPath)){
                return ViewEngineResult.NotFound(appRelativePath, Enumerable.Empty<string>());
            }

            return ViewEngineResult.Found("Default", new NVue(appRelativePath, _viewLocationFormats, null));
        }

        public static string GetNormalizedRouteValue(ActionContext context, string key)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (!context.RouteData.Values.TryGetValue(key, out object routeValue))
            {
                return null;
            }

            string normalizedValue = null;
            if (context.ActionDescriptor.RouteValues.TryGetValue(key, out string value) && !string.IsNullOrEmpty(value))
            {
                normalizedValue = value;
            }

            var stringRouteValue = routeValue?.ToString();
            return string.Equals(normalizedValue, stringRouteValue, StringComparison.OrdinalIgnoreCase) ? normalizedValue : stringRouteValue;
        }

        
    }

    public static class PathHelper
    {
        public static string GetAbsolutePath(string executingFilePath, string pagePath)
        {
            // Path is not valid or a page name; no change required.
            if (string.IsNullOrEmpty(pagePath) || !IsRelativePath(pagePath))
            {
                return pagePath;
            }

            if (IsAbsolutePath(pagePath))
            {
                // An absolute path already; no change required.
                return pagePath.Replace("~/", string.Empty);
            }

            // Given a relative path i.e. not yet application-relative (starting with "~/" or "/"), interpret
            // path relative to currently-executing view, if any.
            if (string.IsNullOrEmpty(executingFilePath))
            {
                // Not yet executing a view. Start in app root.
                return $"/{pagePath}";
            }

            // Get directory name (including final slash) but do not use Path.GetDirectoryName() to preserve path
            // normalization.
            var index = executingFilePath.LastIndexOf('/');
            return executingFilePath.Substring(0, index + 1) + pagePath;
        }

        public static bool IsAbsolutePath(string name) => name.StartsWith("~/") || name.StartsWith("/");

        // Though ./ViewName looks like a relative path, framework searches for that view using view locations.
        public static bool IsRelativePath(string name) => !IsAbsolutePath(name);
    }
}