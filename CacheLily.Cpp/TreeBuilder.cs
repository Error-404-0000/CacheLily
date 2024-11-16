using CacheLily.Cpp;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace CacheLily.Cpp
{
    public class TreeBuilder
    {
        private Stack<CppTree> scopeStack = new Stack<CppTree>();

        public CppTree BuildTree(string cppStr)
        {
            if (string.IsNullOrWhiteSpace(cppStr))
                throw new ArgumentNullException(nameof(cppStr));

            var root = new CppTree(CppNodeType.Namespace, "Global"); // Root node for the tree
            scopeStack.Push(root);

            var lines = cppStr.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            int currentIndex = 0;
            while (currentIndex < lines.Length)
            {
                ParseLine(lines, ref currentIndex);
            }

            return root;
        }

        private void ParseLine(string[] lines, ref int currentIndex)
        {
            if (currentIndex >= lines.Length)
                return;

            string line = lines[currentIndex].Trim();

            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//"))
            {
                currentIndex++;
                return;
            }

            var parent = scopeStack.Peek();

            // Handle include directives
            if (IsIncludeDirective(line, out var includePath))
            {
                var includeNode = new CppTree(CppNodeType.Include, includePath);
                parent.Children.Add(includeNode);
                currentIndex++;
            }
            // Handle class definitions
            else if (IsClassDefinition(line,ref currentIndex, out var classNode))
            {
                parent.Children.Add(classNode);
            }
            // Handle function definitions
            else if (IsFunctionDefinition(lines, ref currentIndex, out var functionNode))
            {
                parent.Children.Add(functionNode);
            }
            // Handle variable declarations
            else if (IsVariableDeclaration(line, out var variableNode))
            {
                parent.Children.Add(variableNode);
                currentIndex++;
            }
            // Handle if statements
            else if (IsIfStatement(lines, ref currentIndex, out var ifNode))
            {
                parent.Children.Add(ifNode);
            }
            // Handle function calls
            else if (IsFunctionCall(line, out var functionCallNode))
            {
                parent.Children.Add(functionCallNode);
                currentIndex++;
            }
            // Handle assignments
            else if (IsAssignment(line, out var assignmentNode))
            {
                parent.Children.Add(assignmentNode);
                currentIndex++;
            }
            // Handle return statements
            else if (IsReturnStatement(line, out var returnNode))
            {
                parent.Children.Add(returnNode);
                currentIndex++;
            }
            else if (line.Contains("{"))
            {
                // Start of a block
                var blockNode = new CppTree(CppNodeType.Block, "Block");
                parent.Children.Add(blockNode);
                scopeStack.Push(blockNode);
                currentIndex++;
            }
            else if (line.Contains("}"))
            {
                // End of a block
                if (scopeStack.Count > 1)
                    scopeStack.Pop();
                currentIndex++;
            }
            else
            {
                currentIndex++;
            }
        }

        // Check for #include directives
        private bool IsIncludeDirective(string line, out string includePath)
        {
            var match = Regex.Match(line, @"^\s*#include\s*([<""][^>""]+[>""])");
            if (match.Success)
            {
                includePath = match.Groups[1].Value;
                return true;
            }
            includePath = string.Empty;
            return false;
        }

        // Check for class definitions
        private bool IsClassDefinition(string line,ref int currentIndex, out CppTree classNode)
        {
            classNode = null;
            var match = Regex.Match(line, @"^(class|struct)\s+(\w+)(\s*:\s*public\s+(\w+))?\s*\{");
            if (match.Success)
            {
                var classType = match.Groups[1].Value; // class or struct
                var className = match.Groups[2].Value;
                var baseClass = match.Groups[4].Value;

                classNode = new CppTree(classType == "class" ? CppNodeType.Class : CppNodeType.Struct, className);

                if (!string.IsNullOrEmpty(baseClass))
                {
                    // Handle inheritance if necessary
                    // For simplicity, we're not adding it to the tree
                }

                currentIndex++;
                scopeStack.Push(classNode);
                return true;
            }
            return false;
        }

        // Check for function definitions
        private bool IsFunctionDefinition(string[] lines, ref int currentIndex, out CppTree functionNode)
        {
            functionNode = null;
            string line = lines[currentIndex].Trim();

            // Match function signatures, e.g., "int main() {"
            var functionPattern = @"^(.*?)\s+(\w+)\s*\(([^)]*)\)\s*(const)?\s*\{";
            var match = Regex.Match(line, functionPattern);
            if (match.Success)
            {
                var returnType = match.Groups[1].Value.Trim();
                var functionName = match.Groups[2].Value.Trim();
                var parameters = match.Groups[3].Value.Trim();
                var isConst = match.Groups[4].Success;

                var parameterList = ParseParameters(parameters);

                functionNode = new CppTree(CppNodeType.Function, functionName)
                {
                    ReturnType = returnType,
                    Parameters = parameterList
                };

                currentIndex++; // Move past the function signature line

                // Enter function block
                scopeStack.Push(functionNode);
                ParseBlock(lines, ref currentIndex);
                scopeStack.Pop();

                return true;
            }
            return false;
        }

        // Check for variable declarations
        private bool IsVariableDeclaration(string line, out CppTree variableNode)
        {
            variableNode = null;
            // Match patterns like "const char* serverIP = "127.0.0.1";"
            var match = Regex.Match(line, @"^(const\s+)?(.+?)\s+(\w+)\s*(=\s*(.+))?;");
            if (match.Success)
            {
                var isConst = match.Groups[1].Success;
                var type = match.Groups[2].Value.Trim();
                var name = match.Groups[3].Value.Trim();
                var value = match.Groups[5].Value.Trim();

                variableNode = new CppTree(CppNodeType.Variable, name)
                {
                    Type = (isConst ? "const " : "") + type,
                    Value = string.IsNullOrEmpty(value) ? null : value
                };
                return true;
            }
            return false;
        }

        // Check for if statements
        private bool IsIfStatement(string[] lines, ref int currentIndex, out CppTree ifNode)
        {
            ifNode = null;
            string line = lines[currentIndex].Trim();

            var match = Regex.Match(line, @"^if\s*\((.*)\)\s*\{");
            if (match.Success)
            {
                string condition = match.Groups[1].Value.Trim();
                ifNode = new CppTree(CppNodeType.IfStatement, "if")
                {
                    Condition = condition
                };

                currentIndex++; // Move past the if statement line

                // Enter if block
                scopeStack.Push(ifNode);
                ParseBlock(lines, ref currentIndex);
                scopeStack.Pop();

                return true;
            }
            return false;
        }

        // Check for function calls
        private bool IsFunctionCall(string line, out CppTree functionCallNode)
        {
            functionCallNode = null;
            var match = Regex.Match(line, @"^(\w+::)?(\w+)\s*\((.*)\)\s*;");
            if (match.Success)
            {
                var functionName = match.Groups[2].Value.Trim();
                var arguments = match.Groups[3].Value.Trim();

                var args = ParseArguments(arguments);

                functionCallNode = new CppTree(CppNodeType.FunctionCall, functionName)
                {
                    Parameters = args
                };
                return true;
            }
            return false;
        }

        // Check for assignments
        private bool IsAssignment(string line, out CppTree assignmentNode)
        {
            assignmentNode = null;
            var match = Regex.Match(line, @"^(.+?)\s*=\s*(.+);$");
            if (match.Success)
            {
                var left = match.Groups[1].Value.Trim();
                var right = match.Groups[2].Value.Trim();

                assignmentNode = new CppTree(CppNodeType.Assignment, left)
                {
                    Value = right
                };
                return true;
            }
            return false;
        }

        // Check for return statements
        private bool IsReturnStatement(string line, out CppTree returnNode)
        {
            returnNode = null;
            var match = Regex.Match(line, @"^return\s+(.*);$");
            if (match.Success)
            {
                var returnValue = match.Groups[1].Value.Trim();
                returnNode = new CppTree(CppNodeType.ReturnStatement, "return")
                {
                    Value = returnValue
                };
                return true;
            }
            return false;
        }

        // Parse blocks
        private void ParseBlock(string[] lines, ref int currentIndex)
        {
            int braceCount = 1; // Already inside a '{'
            while (currentIndex < lines.Length && braceCount > 0)
            {
                string line = lines[currentIndex].Trim();

                if (line.Contains("{"))
                {
                    braceCount += CountOccurrences(line, '{');
                }
                if (line.Contains("}"))
                {
                    braceCount -= CountOccurrences(line, '}');
                }

                if (braceCount > 0)
                {
                    ParseLine(lines, ref currentIndex);
                }
                else
                {
                    currentIndex++; // Move past the closing '}'
                }
            }
        }

        // Helper method to count occurrences of a character
        private int CountOccurrences(string str, char c)
        {
            int count = 0;
            foreach (var ch in str)
            {
                if (ch == c)
                    count++;
            }
            return count;
        }

        // Parse parameter list
        private List<string> ParseParameters(string parameters)
        {
            var paramList = new List<string>();
            if (!string.IsNullOrWhiteSpace(parameters))
            {
                var paramArray = SplitParameters(parameters);

                foreach (var param in paramArray)
                {
                    var trimmedParam = param.Trim();
                    if (!string.IsNullOrEmpty(trimmedParam))
                    {
                        paramList.Add(trimmedParam);
                    }
                }
            }
            return paramList;
        }

        // Helper method to split parameters correctly, considering commas inside templates or parentheses
        private List<string> SplitParameters(string paramStr)
        {
            var paramsList = new List<string>();
            int angleBrackets = 0;
            int parentheses = 0;
            int lastSplit = 0;

            for (int i = 0; i < paramStr.Length; i++)
            {
                if (paramStr[i] == '<')
                    angleBrackets++;
                else if (paramStr[i] == '>')
                    angleBrackets--;
                else if (paramStr[i] == '(')
                    parentheses++;
                else if (paramStr[i] == ')')
                    parentheses--;
                else if (paramStr[i] == ',' && angleBrackets == 0 && parentheses == 0)
                {
                    paramsList.Add(paramStr.Substring(lastSplit, i - lastSplit));
                    lastSplit = i + 1;
                }
            }
            paramsList.Add(paramStr.Substring(lastSplit));

            return paramsList;
        }

        // Parse arguments for function calls
        private List<string> ParseArguments(string arguments)
        {
            return SplitParameters(arguments);
        }
    }
}
