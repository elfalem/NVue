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

namespace NVue{
    public class NVue : IView{
        private string _viewPhysicalPath;

        private string _templateClassName;

        public NVue(string path){
            if(string.IsNullOrWhiteSpace(path)){
                throw new ArgumentNullException();
            }
            _viewPhysicalPath = path;
            _templateClassName = path.Replace("/", string.Empty).Replace(".nvue", string.Empty).Replace("Views", string.Empty) + "Template";
        }

        public string Path => _viewPhysicalPath;

        public Task RenderAsync(ViewContext context)
        {
            var rawTemplate = File.ReadAllText(_viewPhysicalPath);
            // initial naive approach
            //return context.Writer.WriteAsync(rawContents.Replace("{{Message}}", context.ViewData["Message"].ToString()));

            var sourceDocument = TemplateParser.Parse(rawTemplate, context, _templateClassName);

            var assemblyName = System.IO.Path.GetRandomFileName();

            var compilation = Compile(assemblyName, sourceDocument);

            var assembly = LoadAssembly(compilation);

            var baseInstance = Activate(assembly, context);
            
            var renderResult = baseInstance.Execute();

            return context.Writer.WriteAsync(renderResult);
        }

        private CSharpCompilation Compile(string assemblyName, string sourceDocument){
            var sourceText = SourceText.From(sourceDocument.ToString(), Encoding.UTF8);
            var syntaxTree = CSharpSyntaxTree.ParseText(sourceText).WithFilePath(assemblyName);

            var references = new List<PortableExecutableReference>();
            var mscorlib = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
            var baseTemplateReference = MetadataReference.CreateFromFile(typeof(BaseNVueTemplate).Assembly.Location);
            references.Add(mscorlib);
            references.Add(baseTemplateReference);
            // foreach(var val in context.ViewData.Values){
                //references.Add(MetadataReference.CreateFromFile(val.GetType().Assembly.Location)); //most likely needed when referencing a type from another project or nuget package
            // }

            var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);

            return CSharpCompilation.Create(assemblyName, options: compilationOptions, references: references).AddSyntaxTrees(syntaxTree);
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