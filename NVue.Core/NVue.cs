using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;

namespace NVue.Core
{
    public class NVue : IView{
        private string _viewPhysicalPath;

        private string[] _locationFormats;
        private string _controllerName;

        private string _templateClassName;

        public NVue(string path, string[] locationFormats, string controllerName){
            if(string.IsNullOrWhiteSpace(path)){
                throw new ArgumentNullException();
            }
            _viewPhysicalPath = path;
            _templateClassName = path.Replace("/", string.Empty).Replace(".nvue", string.Empty).Replace("Views", string.Empty) + "Template";

            //TODO: need better way to search for layout location
            _locationFormats = locationFormats;
            _controllerName = controllerName;
        }

        public string Path => _viewPhysicalPath;

        public Task RenderAsync(ViewContext context)
        {
            // TODO: reading and parsing entire template just to get layout name
            var rawTemplate = File.ReadAllText(_viewPhysicalPath);
            var templateParser = new TemplateParser(rawTemplate);
            templateParser.Parse();
            var layoutName = templateParser.LayoutTemplateName ?? "_Layout";
            var layoutPath = GetLayoutPath(layoutName);

            string renderResult;
            if(layoutPath == null){
                renderResult = NVueTemplateEngine.RunCompile(rawTemplate, _templateClassName, null, context.ViewData);
            }else{
                var rawLayoutTemplate = File.ReadAllText(layoutPath);
                renderResult = NVueTemplateEngine.RunCompile(rawTemplate, _templateClassName, rawLayoutTemplate, context.ViewData);
            }

            return context.Writer.WriteAsync(renderResult);
        }

        private string GetLayoutPath(string layoutName){
            foreach(var locationFormat in _locationFormats){
                var location = string.Format(locationFormat, layoutName, _controllerName);

                if(File.Exists(location)){
                    return location;
                }
            }
            return null;
        }
    }
}