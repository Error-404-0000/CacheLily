using System;
using System.Collections.Generic;
using System.Text;

namespace CacheLily.Cpp
{
    public enum CppNodeType
    {
        Namespace,
        Include,
        Class,
        Struct,
        Function,
        Variable,
        Assignment,
        FunctionCall,
        IfStatement,
        ReturnStatement,
        Block,
        Template,
        Using,
        Enum,
        Typedef,
        Expression
    }

    public class CppTree
    {
        public CppNodeType NodeType { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? ReturnType { get; set; }
        public string? Type { get; set; }
        public string? Value { get; set; } // For variables and assignments
        public string? Condition { get; set; } // For if statements
        public List<string>? Parameters { get; set; } // For functions and function calls
        public List<CppTree> Children { get; set; } = new List<CppTree>();

        public CppTree(CppNodeType nodeType, string name)
        {
            NodeType = nodeType;
            Name = name;
        }

        // For debugging purposes
        public override string ToString()
        {
            var sb = new StringBuilder();
            ToString(sb, 0);
            return sb.ToString();
        }

        private void ToString(StringBuilder sb, int indentLevel)
        {
            string indent = new string(' ', indentLevel * 2);
            sb.Append(indent);
            sb.Append($"{NodeType}: {Name}");

            // Append additional details based on node type
            switch (NodeType)
            {
                case CppNodeType.Function:
                    sb.Append($" [ReturnType: {ReturnType}; Parameters: {string.Join(", ", Parameters ?? new List<string>())}]");
                    break;
                case CppNodeType.Variable:
                case CppNodeType.Assignment:
                    sb.Append($" [Type: {Type}; Value: {Value}]");
                    break;
                case CppNodeType.FunctionCall:
                    sb.Append($" [Parameters: {string.Join(", ", Parameters ?? new List<string>())}]");
                    break;
                case CppNodeType.IfStatement:
                    sb.Append($" [Condition: {Condition}]");
                    break;
                case CppNodeType.Template:
                    sb.Append($" [Parameters: {string.Join(", ", Parameters ?? new List<string>())}]");
                    break;
                    // Add more cases as needed
            }

            sb.AppendLine();

            foreach (var child in Children)
            {
                child.ToString(sb, indentLevel + 1);
            }
        }
    }
}
