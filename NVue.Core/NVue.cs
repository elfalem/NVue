using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;

namespace NVue.Core{
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
            var rawTemplate = File.ReadAllText(_viewPhysicalPath);
            // initial naive approach
            //return context.Writer.WriteAsync(rawContents.Replace("{{Message}}", context.ViewData["Message"].ToString()));

            var parser = new TemplateParser(rawTemplate);
            parser.Parse();

            var layoutName = parser.LayoutTemplateName ?? "_Layout";
            var layoutPath = GetLayoutPath(layoutName);

            string sourceDocument;
            if(layoutPath == null){
                sourceDocument = parser.GenerateFinalSourceDocument(_templateClassName, context);
            }else{
                var rawLayoutTemplate = File.ReadAllText(layoutPath);
                var layoutParser = new TemplateParser(rawLayoutTemplate, parser.Slots, parser.Scripts);
                layoutParser.Parse();

                sourceDocument = layoutParser.GenerateFinalSourceDocument(_templateClassName, context);
            }

            Console.WriteLine(sourceDocument);

            var assemblyName = System.IO.Path.GetRandomFileName();

            var compilation = Compile(assemblyName, sourceDocument, context);

            var assembly = LoadAssembly(compilation);

            var baseInstance = Activate(assembly, context);
            
            var renderResult = baseInstance.Execute();

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

        private CSharpCompilation Compile(string assemblyName, string sourceDocument, ViewContext context){
            var sourceText = SourceText.From(sourceDocument.ToString(), Encoding.UTF8);
            var syntaxTree = CSharpSyntaxTree.ParseText(sourceText).WithFilePath(assemblyName);


            var referenceLocations = new List<string>();            
            var mscorlibLocation = typeof(object).Assembly.Location;
            var baseTemplateLocation = typeof(BaseNVueTemplate).Assembly.Location;
            referenceLocations.Add(mscorlibLocation);
            referenceLocations.Add(baseTemplateLocation);            
            foreach(var value in context.ViewData.Values){
                GetAssemblyReferences(referenceLocations, value.GetType());
            }

            var references = referenceLocations.Distinct().Select(location => MetadataReference.CreateFromFile(location));

            var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);

            return CSharpCompilation.Create(assemblyName, options: compilationOptions, references: references).AddSyntaxTrees(syntaxTree);
        }

        private void GetAssemblyReferences(List<string> referenceLocations, Type type){
            referenceLocations.Add(type.Assembly.Location);
            if(type.IsGenericType){
                foreach(var genericType in type.GenericTypeArguments){
                    GetAssemblyReferences(referenceLocations, genericType);
                }
            }
        }

        private Assembly LoadAssembly(CSharpCompilation compilation){
            using (var assemblyStream = new MemoryStream())
            {
                var result = compilation.Emit(assemblyStream, null);

                if (!result.Success)
                {
                    throw new Exception(string.Join(" ", result.Diagnostics.Select(d => d + "\n")));
                }

                assemblyStream.Seek(0, SeekOrigin.Begin);

                return Assembly.Load(assemblyStream.ToArray(), null);
            }
        }

        private BaseNVueTemplate Activate(Assembly assembly, ViewContext context){
            var templateInstance = assembly.CreateInstance($"TemplateNamespace.{_templateClassName}");
            var templateType = templateInstance.GetType();

            //TODO: cache the templateInstance and properties metadata so as to not create on every render
            var properties = templateType.GetRuntimeProperties()
                .Where((property) =>
                {
                    return
                        //property.IsDefined(activateAttributeType) &&
                        property.GetIndexParameters().Length == 0 &&
                        property.SetMethod != null &&
                        !property.SetMethod.IsStatic;
            });

            foreach(var prop in properties){
                if(context.ViewData.ContainsKey(prop.Name)){
                    prop.SetValue(templateInstance, context.ViewData[prop.Name]);
                }
            }

            return (BaseNVueTemplate)templateInstance;
        }
    }
}