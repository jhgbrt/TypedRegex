using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace TypedRegex.Generator
{

    class RecordReceiver : ISyntaxReceiver
    {
        public IEnumerable<RecordDeclarationSyntax> CandidateRecords => Records;
        List<RecordDeclarationSyntax> Records { get; } = new();
        HashSet<string> TypesUsedForTypedRegex { get; } = new();
        public void OnVisitSyntaxNode(SyntaxNode syntaxNode) 
        {
            if (syntaxNode is RecordDeclarationSyntax record)
            {
                if (!record.AttributeLists.SelectMany(a => a.Attributes).Any())
                    return;
                if (record.ParameterList is null) // only allow records with primary constructor
                    return;
                Records.Add(record);
            }
        }
    }

    [Generator]
    public class Generator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new RecordReceiver());
        }

        readonly static string attributeText = @"using System;
using System.Text.RegularExpressions;

namespace System.Text.RegularExpressions.Typed
{
    internal class TypedRegexAttribute : Attribute
    {
        public Regex Regex { get; }
        public TypedRegexAttribute(string pattern) => Regex = new Regex(pattern);
    }
}";
        public void Execute(GeneratorExecutionContext context)
        {
            context.AddSource("TypedRegexAttribute.generated.cs", attributeText);

            // we're going to create a new compilation that contains the attribute.
            // TODO: we should allow source generators to provide source during initialize, so that this step isn't required.
            CSharpParseOptions options = (context.Compilation as CSharpCompilation).SyntaxTrees[0].Options as CSharpParseOptions;
            Compilation compilation = context.Compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText(SourceText.From(attributeText, Encoding.UTF8), options));

            if (context.SyntaxReceiver is not RecordReceiver receiver) return;

            var stringSymbol = compilation.GetTypeByMetadataName("System.String");
            var formatProviderSymbol = compilation.GetTypeByMetadataName("System.IFormatProvider");
            var attributeSymbol = compilation.GetTypeByMetadataName("System.Text.RegularExpressions.Typed.TypedRegexAttribute");


            var inconsistentRegex
            = from record in receiver.CandidateRecords
              let model = compilation.GetSemanticModel(record.SyntaxTree)
              let symbol = model.GetDeclaredSymbol(record)
              let constructor = symbol.Constructors.First()
              let attribute = symbol.GetAttributes().FirstOrDefault(a => a.AttributeClass.Equals(attributeSymbol, SymbolEqualityComparer.Default))
              where attribute is not null
              let pattern = (string)attribute.ConstructorArguments.First().Value
              let regex = new Regex(pattern)
              let groupNames = regex.GetGroupNames().Skip(1)
              let propertyNames = constructor.Parameters.Select(p => p.Name)
              let missing = propertyNames.Except(groupNames)
              where missing.Any()
              select (model, regex, missing, record, symbol);

            foreach (var item in inconsistentRegex)
            {
                var descriptor = new DiagnosticDescriptor(
                    "TRX001", 
                    "Incomplete Regex",
                    "RegEx for parsing type {0} is missing named capture groups for properties {1}", 
                    "TypedRegex", DiagnosticSeverity.Error, true);
                context.ReportDiagnostic(Diagnostic.Create(descriptor, item.record.GetLocation(), item.symbol.Name, string.Join(",", item.missing)));
            }

            var q = (
                from record in receiver.CandidateRecords
                let model = compilation.GetSemanticModel(record.SyntaxTree)
                let symbol = model.GetDeclaredSymbol(record)
                let constructor = symbol.Constructors.First()
                let attribute = symbol.GetAttributes().FirstOrDefault(a => a.AttributeClass.Equals(attributeSymbol, SymbolEqualityComparer.Default))
                where attribute is not null
                let pattern = (string)attribute.ConstructorArguments.First().Value // this gets the regex pattern at compile time, so we can check it and emit compiler errors!
                let regex = new Regex(pattern)
                let @namespace = symbol.ContainingNamespace.Name
                let typedName = $"{symbol.Name}TypedRegex"
                let parameters = (
                    from parameter in record.ParameterList.Parameters
                    let parameterSymbol = model.GetDeclaredSymbol(parameter)
                    let parameterType = parameterSymbol.Type
                    let parseMethod = (
                        from method in parameterType.GetMembers("Parse").OfType<IMethodSymbol>()
                        where (method.Parameters.Length == 2
                        && method.Parameters[0].Type.Equals(stringSymbol, SymbolEqualityComparer.Default)
                        && method.Parameters[1].Type.Equals(formatProviderSymbol, SymbolEqualityComparer.Default)
                        ) || (method.Parameters.Length == 1
                        && method.Parameters[0].Type.Equals(stringSymbol, SymbolEqualityComparer.Default)
                        )
                        select method
                    ).FirstOrDefault()
                    let parameterName = parameterSymbol.Name
                    let variableName = parameterName.ToLowerInvariant()
                    select (parameterName, parameterType, parseMethod, variableName)
                ).ToArray()
                select (record, regex, symbol.Name, @namespace, typedName, parameters)
            ).ToArray();

            //var diagnostics = from item in q
            //                  let 

            //context.ReportDiagnostic(Diagnostic.Create(context))



            foreach (var (record, regex, recordName, @namespace, typedName, parameters) in q)
            {
                var sb = new StringBuilder();
                sb
                    .AppendLine($@"using System.Text.RegularExpressions;

namespace {@namespace}
{{
    public partial record {recordName}
    {{
        static readonly Regex _regex = new Regex(@""{regex}"");
        public static {recordName} Parse(string s)
        {{
            var match = _regex.Match(s);");
                foreach (var (parameterName, parameterType, parseMethod, variableName) in parameters)
                {
                    var fullyQualifiedName = parseMethod switch
                    {
                        not null => $"{parameterType.ContainingNamespace.Name}.{parameterType.Name}.{parseMethod.Name}",
                        _ => string.Empty
                    };

                    var assignment = parseMethod switch
                    {
                        { Parameters: { Length: 2 } } 
                            => $@"            var {variableName} = {fullyQualifiedName}(match.Groups[""{parameterName}""].Value, CultureInfo.InvariantCulture);",
                        { Parameters: { Length: 1 } } 
                            => $@"            var {variableName} = {fullyQualifiedName}(match.Groups[""{parameterName}""].Value);",
                        _ 
                            => $@"            var {variableName} = match.Groups[""{parameterName}""].Value;"
                    };
                    sb.AppendLine(assignment);
                }
                sb
                    .Append($"            return new {recordName}(")
                    .Append(string.Join(", ", parameters.Select(p => p.variableName)))
                    .AppendLine(");");
                sb.AppendLine(@$"        }}
    }}
}}");
                context.AddSource($"{recordName}.generated.cs", sb.ToString());
            }

        }
    }
}
