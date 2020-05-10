using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace NVue.Core
{
    public static class NVueTemplateEngine
    {
        public static string RunCompile(string template, string templateName, string layout, object model)
        {
            var modelAsDictionary = model.GetType().GetProperties().ToDictionary(prop => prop.Name, prop => prop.GetValue(model, null));

            return RunCompile(template, templateName, layout, modelAsDictionary);
        }

        public static string RunCompile(string template, string templateName, string layout, IDictionary<string, object> model)
        {
            if(template == null)
            {
                throw new ArgumentNullException($"{nameof(template)} cannot be null.");
            }
            var templateParser = new TemplateParser(template);
            templateParser.Parse();

            SourceText sourceDocument;
            TemplateParser parserToUse;
            if(layout == null){
                sourceDocument = templateParser.GenerateFinalSourceDocument(templateName);
                parserToUse = templateParser;
            }else{
                var layoutParser = new TemplateParser(layout, templateParser.Slots, templateParser.Scripts);
                layoutParser.Parse();

                sourceDocument = layoutParser.GenerateFinalSourceDocument(templateName);
                parserToUse = layoutParser;
            }

            var compilation = CreateCompilation();

            var syntaxTree = CSharpSyntaxTree.ParseText(sourceDocument);
            compilation = compilation.AddSyntaxTrees(syntaxTree);

            var missingProperties = GetMissingProperties(compilation);
            if(missingProperties.Any()){
                var newSourceDocument = parserToUse.GenerateFinalSourceDocument(templateName, missingProperties);
                var newSyntaxTree = CSharpSyntaxTree.ParseText(newSourceDocument);
                compilation = compilation.ReplaceSyntaxTree(syntaxTree, newSyntaxTree);
            }

            var assembly = LoadAssembly(compilation);

            var baseInstance = Activate(assembly, templateName, model);
            
            var renderResult = baseInstance.Execute();

            return renderResult;
        }

        private static CSharpCompilation CreateCompilation(){
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

        private static List<string> GetMissingProperties(CSharpCompilation compilaion){
            //TODO: what if GetMessage() is using a different culture?
            var messages  = compilaion.GetDiagnostics().Where(d => d.Id.Equals("CS0103")).Select(d => d.GetMessage()).Distinct().
                Select(m => m.Replace("The name '", string.Empty).Replace("' does not exist in the current context", string.Empty));

            return messages.ToList();
        }
        private static Assembly LoadAssembly(CSharpCompilation compilation){
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

        private static BaseNVueTemplate Activate(Assembly assembly, string templateName, IDictionary<string, object> model){
            var templateInstance = assembly.CreateInstance($"TemplateNamespace.{templateName}");
            var templateType = templateInstance.GetType();

            //TODO: cache the templateInstance (or assembly) and properties metadata so as to not create on every render
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
                if(model.ContainsKey(prop.Name)){
                    prop.SetValue(templateInstance, model[prop.Name]);
                }
            }

            return (BaseNVueTemplate)templateInstance;
        }
    }
}