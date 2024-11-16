using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Linq;
using CacheLily.Cpp;

namespace CacheLily.Cpp
{
    public class CppToCSharpConverter
    {
        private HashSet<string> ImportedFunctions = new HashSet<string>();
        private string DllName = "libc.so.6"; // Default for Unix-like systems. Adjust as needed.
        private Dictionary<string, string> FunctionReturnTypes = new Dictionary<string, string>()
        {
            // Add return types for known functions
            { "socket", "int" },
            { "connect", "int" },
            { "send", "int" },
            { "recv", "int" },
            { "close", "int" },
            { "htons", "ushort" },
            { "inet_addr", "uint" },
            { "strlen", "size_t" },
            { "memset", "void" }
        };

        public string ConvertToCSharp(CppTree tree)
        {
            var sb = new StringBuilder();

            // Add necessary using directives
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Runtime.InteropServices;");
            sb.AppendLine("using System.Net.Sockets;");
            sb.AppendLine("using System.Text;");
            sb.AppendLine();
            sb.AppendLine("namespace ConvertedNamespace");
            sb.AppendLine("{");

            // Start generating code
            GenerateCSharpCode(tree, sb, 1);

            sb.AppendLine("}");

            return sb.ToString();
        }

        private void GenerateCSharpCode(CppTree node, StringBuilder sb, int indentLevel)
        {
            string indent = new string(' ', indentLevel * 4);

            switch (node.NodeType)
            {
                case CppNodeType.Namespace:
                    if (!string.IsNullOrEmpty(node.Name) && node.Name != "Global")
                    {
                        sb.AppendLine($"{indent}namespace {node.Name}");
                        sb.AppendLine($"{indent}{{");
                        foreach (var child in node.Children)
                        {
                            GenerateCSharpCode(child, sb, indentLevel + 1);
                        }
                        sb.AppendLine($"{indent}}}");
                    }
                    else
                    {
                        foreach (var child in node.Children)
                        {
                            GenerateCSharpCode(child, sb, indentLevel);
                        }
                    }
                    break;

                case CppNodeType.Include:
                    // Typically not needed in C#
                    break;

                case CppNodeType.Class:
                case CppNodeType.Struct:
                    GenerateClassOrStruct(node, sb, indentLevel);
                    break;

                case CppNodeType.Function:
                    GenerateFunction(node, sb, indentLevel);
                    break;

                case CppNodeType.Variable:
                    GenerateVariable(node, sb, indentLevel);
                    break;

                case CppNodeType.Assignment:
                    GenerateAssignment(node, sb, indentLevel);
                    break;

                case CppNodeType.FunctionCall:
                    GenerateFunctionCall(node, sb, indentLevel);
                    break;

                case CppNodeType.IfStatement:
                    GenerateIfStatement(node, sb, indentLevel);
                    break;

                case CppNodeType.ReturnStatement:
                    GenerateReturnStatement(node, sb, indentLevel);
                    break;

                case CppNodeType.Block:
                    sb.AppendLine($"{indent}{{");
                    foreach (var child in node.Children)
                    {
                        GenerateCSharpCode(child, sb, indentLevel + 1);
                    }
                    sb.AppendLine($"{indent}}}");
                    break;

                default:
                    foreach (var child in node.Children)
                    {
                        GenerateCSharpCode(child, sb, indentLevel);
                    }
                    break;
            }
        }

        private void GenerateClassOrStruct(CppTree node, StringBuilder sb, int indentLevel)
        {
            string indent = new string(' ', indentLevel * 4);
            string classType = node.NodeType == CppNodeType.Class ? "class" : "struct";
            sb.AppendLine($"{indent}public {classType} {node.Name}");
            sb.AppendLine($"{indent}{{");

            foreach (var child in node.Children)
            {
                GenerateCSharpCode(child, sb, indentLevel + 1);
            }

            sb.AppendLine($"{indent}}}");
        }

        private void GenerateFunction(CppTree node, StringBuilder sb, int indentLevel)
        {
            string indent = new string(' ', indentLevel * 4);
            string returnType = ConvertCppTypeToCSharp(node.ReturnType);
            string methodName = node.Name;
            string parameters = GenerateParameters(node.Parameters);

            bool requiresUnsafe = RequiresUnsafe(node);
            string unsafeKeyword = requiresUnsafe ? "unsafe " : "";

            sb.AppendLine($"{indent}public static {unsafeKeyword}{returnType} {methodName}({parameters})");
            sb.AppendLine($"{indent}{{");

            foreach (var child in node.Children)
            {
                GenerateCSharpCode(child, sb, indentLevel + 1);
            }

            sb.AppendLine($"{indent}}}");
        }

        private void GenerateVariable(CppTree node, StringBuilder sb, int indentLevel)
        {
            string indent = new string(' ', indentLevel * 4);
            string varType = ConvertCppTypeToCSharp(node.Type);
            string varName = node.Name;
            string valueAssignment = string.Empty;

            if (!string.IsNullOrEmpty(node.Value))
            {
                valueAssignment = $" = {TranslateExpression(node.Value)}";
            }

            bool requiresUnsafe = RequiresUnsafe(node);
            string unsafeKeyword = requiresUnsafe ? "unsafe " : "";

            sb.AppendLine($"{indent}{unsafeKeyword}{varType} {varName}{valueAssignment};");
        }

        private void GenerateAssignment(CppTree node, StringBuilder sb, int indentLevel)
        {
            string indent = new string(' ', indentLevel * 4);
            string left = TranslateExpression(node.Name);
            string right = TranslateExpression(node.Value);
            sb.AppendLine($"{indent}{left} = {right};");
        }

        private void GenerateFunctionCall(CppTree node, StringBuilder sb, int indentLevel)
        {
            string indent = new string(' ', indentLevel * 4);
            string functionName = node.Name;
            string arguments = GenerateArguments(node.Parameters);

            if (FunctionReturnTypes.ContainsKey(functionName))
            {
                // Declare P/Invoke if not already
                if (!ImportedFunctions.Contains(functionName))
                {
                    sb.AppendLine($"{indent}[DllImport(\"{DllName}\", CallingConvention = CallingConvention.Cdecl)]");
                    string returnType = ConvertCppTypeToCSharp(FunctionReturnTypes[functionName]);
                    string pInvokeParameters = GenerateParameters(node.Parameters, forPInvoke: true);
                    sb.AppendLine($"{indent}public static extern {returnType} {functionName}({pInvokeParameters});");
                    ImportedFunctions.Add(functionName);
                }

                // Determine if the function has a return value
                string returnTypeMapped = ConvertCppTypeToCSharp(FunctionReturnTypes[functionName]);
                if (returnTypeMapped != "void")
                {
                    sb.AppendLine($"{indent}{returnTypeMapped} result = {functionName}({arguments});");
                }
                else
                {
                    sb.AppendLine($"{indent}{functionName}({arguments});");
                }
            }
            else
            {
                // Assume it's a user-defined method within the C# code
                sb.AppendLine($"{indent}{functionName}({arguments});");
            }
        }

        private void GenerateIfStatement(CppTree node, StringBuilder sb, int indentLevel)
        {
            string indent = new string(' ', indentLevel * 4);
            string condition = TranslateExpression(node.Condition);
            sb.AppendLine($"{indent}if ({condition})");
            sb.AppendLine($"{indent}{{");

            foreach (var child in node.Children)
            {
                GenerateCSharpCode(child, sb, indentLevel + 1);
            }

            sb.AppendLine($"{indent}}}");
        }

        private void GenerateReturnStatement(CppTree node, StringBuilder sb, int indentLevel)
        {
            string indent = new string(' ', indentLevel * 4);
            string returnValue = TranslateExpression(node.Value);
            sb.AppendLine($"{indent}return {returnValue};");
        }

        private string GenerateParameters(List<string>? parameters, bool forPInvoke = false)
        {
            if (parameters == null || parameters.Count == 0)
                return "";

            var paramList = new List<string>();
            foreach (var param in parameters)
            {
                // Split parameter into type and name
                var parts = param.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    var type = ConvertCppTypeToCSharp(parts[0]);
                    var name = parts[1];
                    if (forPInvoke)
                    {
                        // For P/Invoke, use appropriate marshaling if necessary
                        paramList.Add($"{type} {name}");
                    }
                    else
                    {
                        paramList.Add($"{type} {name}");
                    }
                }
                else
                {
                    // Handle parameters without names (rare in C++)
                    var type = ConvertCppTypeToCSharp(parts[0]);
                    var name = $"param{paramList.Count}";
                    paramList.Add($"{type} {name}");
                }
            }
            return string.Join(", ", paramList);
        }

        private string GenerateArguments(List<string>? arguments)
        {
            if (arguments == null || arguments.Count == 0)
                return "";

            var args = new List<string>();
            foreach (var arg in arguments)
            {
                args.Add(TranslateExpression(arg));
            }
            return string.Join(", ", args);
        }

        private string TranslateExpression(string expression)
        {
            if (string.IsNullOrEmpty(expression))
                return "";

            // Basic translation of expressions
            expression = expression.Replace("->", ".")
                                   .Replace("::", ".")
                                   .Replace("NULL", "null")
                                   .Replace("TRUE", "true")
                                   .Replace("FALSE", "false");

            // Handle string literals
            expression = Regex.Replace(expression, @"\""(.*?)\""", m => $"\"{m.Groups[1].Value}\"");

            // Handle casts (e.g., (struct sockaddr*)&serverAddress)
            expression = Regex.Replace(expression, @"\((.*?)\)\s*(\w+)", m =>
            {
                var type = m.Groups[1].Value.Trim();
                var var = m.Groups[2].Value.Trim();
                string csharpType = ConvertCppTypeToCSharp(type);
                return $"({csharpType}){var}";
            });

            // Handle sizeof operator
            expression = Regex.Replace(expression, @"sizeof\s*\((.*?)\)", m => $"sizeof({ConvertCppTypeToCSharp(m.Groups[1].Value.Trim())})");

            return expression;
        }

        private bool RequiresUnsafe(CppTree node)
        {
            // Determine if the node or any of its children require unsafe code
            if (!string.IsNullOrEmpty(node.Type) && (node.Type.Contains("*") || node.Type.Contains("&")))
                return true;

            foreach (var child in node.Children)
            {
                if (RequiresUnsafe(child))
                    return true;
            }

            return false;
        }

        private string ConvertCppTypeToCSharp(string cppType)
        {
            if (string.IsNullOrEmpty(cppType))
                return "void";

            bool isPointer = cppType.Contains("*");
            bool isReference = cppType.Contains("&");

            // Clean the type string
            cppType = cppType.Replace("*", "").Replace("&", "").Replace("const", "").Trim();

            // Map basic types
            string csharpType = cppType switch
            {
                "int" => "int",
                "unsigned" => "uint",
                "unsigned int" => "uint",
                "long" => "long",
                "unsigned long" => "ulong",
                "short" => "short",
                "unsigned short" => "ushort",
                "char" => "char",
                "unsigned char" => "byte",
                "float" => "float",
                "double" => "double",
                "bool" => "bool",
                "size_t" => "UIntPtr",
                "std::string" => "string",
                "string" => "string",
                "struct sockaddr_in" => "sockaddr_in",
                "std::function<void(const T&)>" => "Action<T>",
                _ => "IntPtr" // Default to IntPtr for unknown types
            };

            // Apply pointer/reference modifiers
            if (isPointer && csharpType != "string")
                csharpType += "*";

            if (isReference && csharpType != "string")
                csharpType = "ref " + csharpType;

            return csharpType;
        }
    }
}
