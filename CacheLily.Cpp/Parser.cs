using CacheLily.Cpp;


namespace CacheLily.Cpp
{
    public class Parser
    {
        public string Parse(string cpp)
        {
            ArgumentNullException.ThrowIfNullOrWhiteSpace(cpp);
            CppTree Tree = new TreeBuilder().BuildTree(cpp);
            //Console.WriteLine("TREE:");
            //Console.WriteLine(Tree);
            //Console.WriteLine("TREE END");
            CppToCSharpConverter Converter = new CppToCSharpConverter();
            return Converter.ConvertToCSharp(Tree);

        }
    }
}