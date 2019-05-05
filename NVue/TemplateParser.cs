using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace NVue{
    public static class TemplateParser{
        public static string Parse(string template, ViewContext context, string templateClassName){
            var sourceDocument = new StringBuilder();

            var templateDoc = new HtmlDocument();
            templateDoc.LoadHtml(template);

            sourceDocument.AppendLine($@"
namespace TemplateNamespace{{
    public class {templateClassName} : NVue.BaseNVueTemplate
    {{
        private System.Text.StringBuilder _output = new System.Text.StringBuilder();");

            //TODO: need to ensure template references the field (existence in ViewData is not enough)
            foreach(var pair in context.ViewData){
                if(pair.Value != null){
                    sourceDocument.AppendLine($"public {DeclarationFromType(pair.Value.GetType())} {pair.Key} {{get; set;}}");
                }
            }

            var scripts = templateDoc.DocumentNode.SelectNodes("/script[@type=\"text/csharp\"]");
            foreach(var script in scripts){
                sourceDocument.AppendLine(script.InnerText);
            }

            sourceDocument.AppendLine(@"
        public string Execute()
        {
            ");
            
            BuildExecuteBody(sourceDocument, templateDoc, template, context);

            sourceDocument.AppendLine(@"
            return _output.ToString();
        }

        private void Write(object val){
            _output.Append(val);
        }
    }
}
            ");

            Console.WriteLine(sourceDocument.ToString());
            return sourceDocument.ToString();
        }

        private static void BuildExecuteBody(StringBuilder sourceDocument, HtmlDocument templateDoc, string template, ViewContext context){
            var rootNode = templateDoc.DocumentNode.SelectSingleNode("//template");

            if(rootNode != null){
                ProcessNode(sourceDocument, rootNode, template);
            }
        }

        private static void ProcessNode(StringBuilder sourceDocument, HtmlNode node, string template){
            if(node.NodeType == HtmlNodeType.Element){
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
                        ProcessNode(sourceDocument, child, template);
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

            }else if(node.NodeType == HtmlNodeType.Text){
                var textValue = node.OuterHtml;
                if(string.IsNullOrWhiteSpace(textValue)){
                    sourceDocument.AppendLine($"Write(@\"{textValue}\");");
                }else{
                    var literals = Regex.Split(textValue, @"\{\{[^{}]+\}\}").Where(l => l.Length > 0);
                    foreach(var literal in literals){
                        var literalStart = textValue.IndexOf(literal);
                        if(literalStart == 0){
                            sourceDocument.AppendLine($"Write(@\"{literal}\");");
                            textValue = textValue.Substring(literal.Length);
                        }else{
                            var mustacheTag = textValue.Substring(0, literalStart);
                            sourceDocument.AppendLine($"Write({mustacheTag.TrimStart('{').TrimEnd('}')});");

                            sourceDocument.AppendLine($"Write(@\"{literal}\");");
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

        private static string DeclarationFromType(Type type){
            var fullName = type.FullName;

            if(type.IsGenericType){
                var generics = type.GenericTypeArguments.Select(g => DeclarationFromType(g));
                fullName = fullName.Substring(0, fullName.IndexOf("`"));
                return $"{fullName}<{string.Join(", ", generics)}>";
            }

            return fullName;
        }
    }
}