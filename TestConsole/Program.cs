using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TypedRegex.Generator;

namespace TestConsole
{
    
    class Program
    {
        static void Main(string[] args)
        {
            var directory = new DirectoryInfo(Environment.CurrentDirectory);
            while (directory.FullName.Length > 0 && !directory.EnumerateDirectories().Any(d => d.Name == "ConsoleApp"))
                directory = directory.Parent;
            
            string source = File.ReadAllText(Path.Combine(directory.FullName, @"ConsoleApp\Program.cs"));


            var (diagnostics, output) = GetGeneratedOutput(source);

            if (diagnostics.Length > 0)
            {
                Console.WriteLine("Diagnostics:");
                foreach (var diag in diagnostics)
                {
                    Console.WriteLine("   " + diag.ToString());
                }
                Console.WriteLine();
                Console.WriteLine("Output:");
            }

            foreach (var s in output)
                Console.WriteLine(s);
        }

        private static (ImmutableArray<Diagnostic>, string[]) GetGeneratedOutput(string source)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(source);

            var references = new List<MetadataReference>();
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                if (!assembly.IsDynamic)
                {
                    references.Add(MetadataReference.CreateFromFile(assembly.Location));
                }
            }

            var compilation = CSharpCompilation.Create("foo", new SyntaxTree[] { syntaxTree }, references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            // TODO: Uncomment these lines if you want to return immediately if the injected program isn't valid _before_ running generators
            //
            // ImmutableArray<Diagnostic> compilationDiagnostics = compilation.GetDiagnostics();
            //
            // if (diagnostics.Any())
            // {
            //     return (diagnostics, "");
            // }

            ISourceGenerator generator = new Generator();

            var driver = CSharpGeneratorDriver.Create(generator);
            driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var generateDiagnostics);

            return (generateDiagnostics, outputCompilation.SyntaxTrees.Select(s => s.ToString()).ToArray());
        }
    }
}

