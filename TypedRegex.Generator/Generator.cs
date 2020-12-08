using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TypedRegex.Generator
{

    class RecordReceiver : ISyntaxReceiver
    {
        public IEnumerable<RecordDeclarationSyntax> CandidateRecords => Records.Where(r => TypesUsedForTypedRegex.Contains(r.Identifier.Text));
        List<RecordDeclarationSyntax> Records { get; } = new();
        HashSet<string> TypesUsedForTypedRegex { get; } = new();
        public void OnVisitSyntaxNode(SyntaxNode syntaxNode) 
        {
            if (syntaxNode is RecordDeclarationSyntax record)
            {
                Records.Add(record);
            }

            if (syntaxNode is InvocationExpressionSyntax invocation 
                && invocation.Expression is MemberAccessExpressionSyntax memberAccess
                && memberAccess.Expression is IdentifierNameSyntax g
                && g.Identifier.Text is "TypedRegex" 
                )
            {
                TypesUsedForTypedRegex.Add(memberAccess.ChildNodes().OfType<IdentifierNameSyntax>().Last().Identifier.Text);
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

        public void Execute(GeneratorExecutionContext context)
        {
            Compilation compilation = context.Compilation;

            if (context.SyntaxReceiver is not RecordReceiver receiver) return;

            var stringSymbol = compilation.GetTypeByMetadataName("System.String");
            var formatProviderSymbol = compilation.GetTypeByMetadataName("System.IFormatProvider");

            var q = (
                from record in receiver.CandidateRecords
                    let model = compilation.GetSemanticModel(record.SyntaxTree)
                    let symbol = model.GetDeclaredSymbol(record)
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
                    select (record, symbol.Name, @namespace, typedName, parameters)
            ).ToArray();

            var usings = new[]{
                "System",
                "System.Linq",
                "System.Globalization",
                "System.Text.RegularExpressions"
            }.Concat(q.Select(s => s.@namespace).Distinct());

            var sb = new StringBuilder();
            foreach (var u in usings) sb.Append("using ").Append(u).Append(";").AppendLine();

            sb.AppendLine(@"
namespace System.Text.RegularExpressions.Typed
{
    internal abstract class TypedRegex<T>
    {
        protected Regex _regex;
        internal abstract T Match(string input);
        protected abstract string[] PropertyNames { get; }
        protected TypedRegex(string pattern)
        {
            var regex = new Regex(pattern);
            var groupNames = regex.GetGroupNames().Skip(1);
            var missingCaptureGroups = PropertyNames.Except(groupNames);
            if (missingCaptureGroups.Any())
            {
                throw new FormatException($""The regex does not contain capture groups for properties {string.Join("","", missingCaptureGroups)}. The regular expression contained the following groups: '{string.Join("","", groupNames)}'"");
            }
            _regex = regex;
        }
    }

    internal class TypedRegex
    {");

            foreach (var (record, recordName, _, typedName, parameters) in q)
            {
                var parameterNames = string.Join(", ", parameters.Select(p => $"\"{p.parameterName}\"").OrderBy(n => n));

                sb
                    .AppendLine($@"
        internal static TypedRegex<{recordName}> {recordName}(string pattern) => new {typedName}(pattern);
        
        class {typedName} : TypedRegex<{recordName}>
        {{
            public {typedName}(string pattern) : base(pattern) {{ }}
            
            protected override string[] PropertyNames => new[] {{ {parameterNames} }};

            internal override {recordName} Match(string input)
            {{
                var match = _regex.Match(input);");

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
                            => $@"                var {variableName} = {fullyQualifiedName}(match.Groups[""{parameterName}""].Value, CultureInfo.InvariantCulture);",
                        { Parameters: { Length: 1 } } 
                            => $@"                var {variableName} = {fullyQualifiedName}(match.Groups[""{parameterName}""].Value);",
                        _ 
                            => $@"                var {variableName} = match.Groups[""{parameterName}""].Value;"
                    };
                    sb.AppendLine(assignment);
                }
                sb
                    .Append($"                return new {recordName}(")
                    .Append(string.Join(", ", parameters.Select(p => p.variableName)))
                    .AppendLine(");")
                    .AppendLine(@"
            }
        }");
            }

            sb.AppendLine(@"    }
}");

            context.AddSource("TypedRegex.generated.cs", sb.ToString());
        }
    }
}
