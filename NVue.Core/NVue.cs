using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CSharp.RuntimeBinder;

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

            var templateParser = new TemplateParser(rawTemplate);
            templateParser.Parse();

            var layoutName = templateParser.LayoutTemplateName ?? "_Layout";
            var layoutPath = GetLayoutPath(layoutName);

            SourceText sourceDocument;
            TemplateParser parserToUse;
            if(layoutPath == null){
                sourceDocument = templateParser.GenerateFinalSourceDocument(_templateClassName);
                parserToUse = templateParser;
            }else{
                var rawLayoutTemplate = File.ReadAllText(layoutPath);
                var layoutParser = new TemplateParser(rawLayoutTemplate, templateParser.Slots, templateParser.Scripts);
                layoutParser.Parse();

                sourceDocument = layoutParser.GenerateFinalSourceDocument(_templateClassName);
                parserToUse = layoutParser;
            }

            //Console.WriteLine(sourceDocument);

            var compilation = CreateCompilation();

            var syntaxTree = CSharpSyntaxTree.ParseText(sourceDocument);
            compilation = compilation.AddSyntaxTrees(syntaxTree);

            var missingProperties = GetMissingProperties(compilation);
            if(missingProperties.Any()){
                var newSourceDocument = parserToUse.GenerateFinalSourceDocument(_templateClassName, missingProperties);
                var newSyntaxTree = CSharpSyntaxTree.ParseText(newSourceDocument);
                compilation = compilation.ReplaceSyntaxTree(syntaxTree, newSyntaxTree);
            }

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

        private CSharpCompilation CreateCompilation(){
            var assemblyLocations = new List<string>{
                typeof(object).Assembly.Location,
                typeof(BaseNVueTemplate).Assembly.Location,

                // the following are needed for dynamic keyword support
                Assembly.Load(new AssemblyName("System.Linq.Expressions")).Location,
                Assembly.Load(new AssemblyName("Microsoft.CSharp")).Location,
                Assembly.Load(new AssemblyName("System.Runtime")).Location,
                Assembly.Load(new AssemblyName("netstandard")).Location
            };

            var references = assemblyLocations.Select(location => MetadataReference.CreateFromFile(location));

            var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);

            var assemblyName = System.IO.Path.GetRandomFileName();

            return CSharpCompilation.Create(assemblyName, options: compilationOptions, references: references);
        }

        private List<string> GetMissingProperties(CSharpCompilation compilaion){
            //TODO: what if GetMessage() is using a different culture?
            var messages  = compilaion.GetDiagnostics().Where(d => d.Id.Equals("CS0103")).Select(d => d.GetMessage()).Distinct().
                Select(m => m.Replace("The name '", string.Empty).Replace("' does not exist in the current context", string.Empty));

            return messages.ToList();
        }
        private Assembly LoadAssembly(CSharpCompilation compilation){
            using (var assemblyStream = new MemoryStream())
            {
                var result = compilation.Emit(assemblyStream, null);

                if (!result.Success)
                {
                    throw new Exception(string.Join(" ", result.Diagnostics.Select(d => d.Id + "-" + d.GetMessage() + "\n")));
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