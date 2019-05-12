using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace NVue.Core{

    public class TemplateParser{

        private string template;
        private HtmlDocument templateDoc;
        public Dictionary<string, string> Slots;
        public List<string> Scripts;

        public string LayoutTemplateName;

        public TemplateParser(string template, Dictionary<string, string> slots = null, List<string> scripts = null){
            this.template = template;
            templateDoc = new HtmlDocument();
            Slots = slots ?? new Dictionary<string, string>();
            Scripts = scripts ?? new List<string>();
        }
        public void Parse(){
            templateDoc.LoadHtml(template);

            ParseScripts();
            ParseTemplateTag();
        }

        public SourceText GenerateFinalSourceDocument(string templateClassName, List<string> Properties = null){

            var sourceDocument = new StringBuilder();

            sourceDocument.AppendLine($@"
namespace TemplateNamespace{{
    public class {templateClassName} : NVue.Core.BaseNVueTemplate
    {{
        private System.Text.StringBuilder _output = new System.Text.StringBuilder();");

            if(Properties != null){
                foreach(var property in Properties){
                    sourceDocument.AppendLine($"public dynamic {property} {{get; set;}}");
                }
            }

            foreach(var script in Scripts){
                sourceDocument.AppendLine(script);
            }

            sourceDocument.AppendLine(@"
        public string Execute()
        {
            ");
            
            sourceDocument.AppendLine(Slots["default"]);

            sourceDocument.AppendLine(@"
            return _output.ToString();
        }

        private void Write(object val){
            _output.Append(val);
        }
    }
}
            ");

            return SourceText.From(sourceDocument.ToString(), Encoding.UTF8);
        }

        private void ParseScripts(){
            var scriptNodes = templateDoc.DocumentNode.SelectNodes("/script[@type=\"text/csharp\"]");
            if(scriptNodes != null){
                Scripts.AddRange(scriptNodes.Select(scriptNode => scriptNode.InnerText).ToList());
            }
        }

        private void ParseTemplateTag(){
            var sourceDocument = new StringBuilder();
            var rootNode = templateDoc.DocumentNode.SelectSingleNode("//template");

            if(rootNode != null){
                if(rootNode.Attributes.Contains("layout")){
                    LayoutTemplateName = rootNode.Attributes["layout"].Value;
                }
                ProcessNode(sourceDocument, rootNode);
            }

            Slots["default"] = sourceDocument.ToString();
        }

        private void ProcessNode(StringBuilder sourceDocument, HtmlNode node){
            //TODO: DOCTYPE declaration on html tag is not retained
            if(node.NodeType == HtmlNodeType.Element){
                var slotAttribute = node.Attributes.Where(a => a.Name.StartsWith("v-slot:")).SingleOrDefault();
                if(slotAttribute != null){
                    var slotName = slotAttribute.Name.Substring(slotAttribute.Name.IndexOf(":") + 1);
                    //TODO: ensure slot is not called "default"
                    node.Attributes[slotAttribute.Name].Remove();

                    var slotSourceDoc = new StringBuilder();
                    ProcessElementNode(slotSourceDoc, node);
                    Slots.Add(slotName, slotSourceDoc.ToString());
                }else if(node.Name == "slot"){
                    // slot names are not case sensitive (because that's how HAP treats attributes)
                    var slotName = node.Attributes.Contains("name") ? node.Attributes["name"].Value.ToLower() : "default";
                    if(Slots.ContainsKey(slotName)){
                        sourceDocument.AppendLine(Slots[slotName]);
                    }else{
                        // use default slot content if nothing is provided
                        foreach(var childNode in node.ChildNodes){
                            ProcessNode(sourceDocument, childNode);
                        }
                    }
                }else{
                    ProcessElementNode(sourceDocument, node);
                }
            }else if(node.NodeType == HtmlNodeType.Text){
                ProcessTextNode(sourceDocument, node);
            }
        }

        private void ProcessElementNode(StringBuilder sourceDocument, HtmlNode node){
            //TODO: there are whitespace incosistencies with the use of mulitple source documents, attempt to adhere to whitespace from template
            var isTemplateNode = node.Name == "template";
            var attributes = node.Attributes;
            var attributeText = new StringBuilder();
            string loop = null;
            string ifCheck = null;
            string elseIfCheck = null;
            bool elseCheck = false;
            string padding = string.Empty;
            var getPadding = false;
            foreach(var attribute in attributes){
                
                if(attribute.Name == "v-for"){
                    loop = attribute.Value;
                    getPadding = true;
                }else if(attribute.Name == "v-if" || attribute.Name == "v-show"){
                    ifCheck = attribute.Value;
                    getPadding = true;
                }else if(attribute.Name == "v-else-if"){
                    elseIfCheck = attribute.Value;
                    getPadding = true;
                }else if(attribute.Name == "v-else"){
                    elseCheck = true;
                    getPadding = true;
                }else if(attribute.Name.StartsWith(":") || attribute.Name.StartsWith("v-bind:")){
                    var attributeName = attribute.Name.Substring(attribute.Name.IndexOf(":") + 1);
                    var attributeValue = $"\" + ({attribute.Value}) + \"";
                    attributeText.Append($" {attributeName}=\\\"{attributeValue}\\\"");
                }else{
                    attributeText.Append($" {attribute.Name}=\\\"{attribute.Value}\\\"");
                }
            }

            //TODO: error out if more than one of "if, elseif, else" attributes are on the same element

            if(getPadding){
                var precedingCode = template.Substring(0, node.StreamPosition);
                var lastNewline = precedingCode.LastIndexOf('\n');
                if(lastNewline > -1){
                    padding = precedingCode.Substring(lastNewline);
                }


                if(padding.Length > 0){
                    //remove padding to avoid duplication, 12 chars for Write(@"");\n - TODO: what if \r\n ?
                    sourceDocument.Remove(sourceDocument.Length - (padding.Length + 12), padding.Length + 12);
                }
            }

            if(loop != null){
                sourceDocument.AppendLine($"foreach({loop}){{");
            }

            if(ifCheck != null){
                sourceDocument.AppendLine($"if({ifCheck}){{");
            }

            if(elseIfCheck != null){
                sourceDocument.AppendLine($"else if({elseIfCheck}){{");
            }

            if(elseCheck){
                sourceDocument.AppendLine($"else{{");
            }
            
            if(getPadding){
                sourceDocument.AppendLine($"Write(@\"{padding}\");");
            }

            var children = node.ChildNodes;
            
            if(children.Count() > 0 || 
                //prevent tags with empty elements from becoming self-closing
                template.IndexOf("><", node.StreamPosition) == template.IndexOf(">", node.StreamPosition)){
                if(!isTemplateNode){
                    sourceDocument.AppendLine($"Write(\"<{node.Name}{attributeText}>\");");
                }
                foreach(var child in children){
                    ProcessNode(sourceDocument, child);
                }
                if(!isTemplateNode){
                    sourceDocument.AppendLine($"Write(\"</{node.Name}>\");");
                }
            }else if(!isTemplateNode){
                sourceDocument.AppendLine($"Write(\"<{node.Name}{attributeText}/>\");");
            }

            if(ifCheck != null || elseIfCheck != null || elseCheck){
                sourceDocument.AppendLine($"}}");
            }

            if(loop != null){
                sourceDocument.AppendLine($"}}");
            }
        }

        private void ProcessTextNode(StringBuilder sourceDocument, HtmlNode node){
            var textValue = node.OuterHtml;
            if(string.IsNullOrWhiteSpace(textValue)){
                sourceDocument.AppendLine($"Write(@\"{textValue}\");");
            }else{
                var literals = Regex.Split(textValue, @"\{\{[^{}]+\}\}").Where(l => l.Length > 0);
                foreach(var literal in literals){
                    var literalStart = textValue.IndexOf(literal);
                    if(literalStart == 0){
                        sourceDocument.AppendLine($"Write(@\"{literal.Replace("\\{","{").Replace("\\}","}")}\");");
                        textValue = textValue.Substring(literal.Length);
                    }else{
                        var mustacheTag = textValue.Substring(0, literalStart);
                        sourceDocument.AppendLine($"Write({mustacheTag.TrimStart('{').TrimEnd('}')});");

                        sourceDocument.AppendLine($"Write(@\"{literal.Replace("\\{","{").Replace("\\}","}")}\");");
                        textValue = textValue.Substring(mustacheTag.Length + literal.Length);
                    }
                }
                if(textValue.Length > 0){
                    var lastMustache = textValue;
                    sourceDocument.AppendLine($"Write({lastMustache.TrimStart('{').TrimEnd('}')});");
                }
            }
        }
    }
}