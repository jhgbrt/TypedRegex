using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Text.RegularExpressions;

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
                //&& invocation.ArgumentList is ArgumentListSyntax arglist 
                //&& arglist.Arguments != null 
                //&& arglist.Arguments.Count == 1
                //&& arglist.Arguments.Single().Expression is LiteralExpressionSyntax literal
                //&& literal.Token.Kind() == SyntaxKind.StringLiteralToken
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

            if (!(context.SyntaxReceiver is RecordReceiver receiver)) return;

            ITypeSymbol stringSymbol = compilation.GetTypeByMetadataName("System.String");
            ITypeSymbol formatProviderSymbol = compilation.GetTypeByMetadataName("System.IFormatProvider");


            //var recordsByName = receiver.CandidateRecords.ToDictionary(r => r.Identifier.Text);

            //var lookup = (
            //    from p in receiver.TypeLiteralExpressions
            //    let type = p.type
            //    let literal = p.literal.Token.Text
            //    let record = receiver.CandidateRecords.FirstOrDefault(r => r.Identifier.Text == type.Identifier.Text)
            //    where record != null
            //    select (record, literal)
            //    ).ToLookup(x => x.record, x => x.literal);


            //RecordDeclarationSyntax r = null;
            //var l = lookup[r];
            //Debugger.Break();
            //foreach (var item in lookup)
            //{
            //    var record = item.Key;
            //    foreach (var literal in item)
            //    {
            //        var regex = new Regex(literal);
            //        var groupNames = regex.GetGroupNames().Skip(1); // skip group "0"
            //        var parameters = record.ParameterList.Parameters.Select(p => p.Identifier.Text);
            //        if ((from g in groupNames
            //            join p in parameters on g equals p
            //            select (g, p)).Count() != groupNames.Count())
            //        {
            //            Debugger.Break();
            //        }
            //        Debugger.Break();
            //    }
            //}

            //foreach (var (type, literal) in receiver.TypeLiteralExpressions)
            //{
            //    Debugger.Break();
            //    var record = receiver.CandidateRecords.FirstOrDefault(r => r.Identifier.Text == type.Identifier.Text);

            //}

            List<(RecordDeclarationSyntax, INamedTypeSymbol)> records = new();
            foreach (var record in receiver.CandidateRecords)
            {
                SemanticModel model = compilation.GetSemanticModel(record.SyntaxTree);
                INamedTypeSymbol recordSymbol = model.GetDeclaredSymbol(record);
                records.Add((record, recordSymbol));
            }

            var sb = new StringBuilder();
            sb
                .AppendLine("using System;")
                .AppendLine("using System.Linq;")
                .AppendLine("using System.Globalization;")
                .AppendLine("using System.Text.RegularExpressions;");

            foreach (var namespaceName in records.Select(s => s.Item2.ContainingNamespace.Name).Distinct())
                sb.AppendLine($"using {namespaceName};");

            sb
                .AppendLine("namespace System.Text.RegularExpressions.Typed")
                .AppendLine("{")
                .AppendLine($"    internal abstract class TypedRegex<T>")
                .AppendLine("    {")
                .AppendLine("        protected Regex _regex;")
                .AppendLine("        internal abstract T Match(string input);")
                .AppendLine("        protected TypedRegex(string pattern) => _regex = new Regex(pattern);")
                .AppendLine("    }")
                .AppendLine($"    internal class TypedRegex")
                .AppendLine("    {");

            foreach (var (record, recordSymbol) in records)
            {
                var typedName = $"{recordSymbol.Name}TypedRegex";
                sb
                    .AppendLine($"        internal static TypedRegex<{recordSymbol.Name}> {recordSymbol.Name}(string pattern) => new {typedName}(pattern);")
                    .AppendLine($"        class {typedName} : TypedRegex<{recordSymbol.Name}>")
                    .AppendLine( "        {")
                    .AppendLine($"            public {typedName}(string pattern) : base(pattern) {{ }}")
                    .AppendLine($"            internal override {recordSymbol.Name} Match(string input)")
                    .AppendLine("            {")
                    .AppendLine("                var match = _regex.Match(input);");

                SemanticModel model = compilation.GetSemanticModel(record.SyntaxTree);
                foreach (var parameter in record.ParameterList.Parameters)
                {
                    var parameterSymbol = model.GetDeclaredSymbol(parameter);
                    var parameterType = parameterSymbol.Type;

                    var parse = (
                        from parseMethod in parameterType.GetMembers("Parse").OfType<IMethodSymbol>()
                        where parseMethod.Parameters.Length == 2
                        && parseMethod.Parameters.First().Type.Equals(stringSymbol, SymbolEqualityComparer.Default)
                        && parseMethod.Parameters.Skip(1).First().Type.Equals(formatProviderSymbol, SymbolEqualityComparer.Default)
                        select parseMethod
                    ).SingleOrDefault();

                    sb.AppendLine($"                if (!_regex.GetGroupNames().Any(s => s == \"{parameterSymbol.Name}\")) throw new FormatException(\"Regex does not contain capture group for {parameterSymbol.Name}\");");
                    if (parse is null)
                        sb.AppendLine($"                var {parameterSymbol.Name.ToLowerInvariant()} = match.Groups[\"{parameterSymbol.Name}\"].Value;");
                    else 
                        sb.AppendLine($"                var {parameterSymbol.Name.ToLowerInvariant()} = {parameterType.ContainingNamespace.Name}.{parameterType.Name}.{parse.Name}(match.Groups[\"{parameterSymbol.Name}\"].Value, CultureInfo.InvariantCulture);");
                }
                sb
                    .Append($"                var result = new {recordSymbol.Name}(")
                    .Append(string.Join(",", record.ParameterList.Parameters.Select(p => model.GetDeclaredSymbol(p)).Select(p => p.Name.ToLowerInvariant())))
                    .AppendLine(");")
                    .AppendLine("                return result;")
                    .AppendLine("            }")
                    .AppendLine("        }");
            }
            sb.AppendLine("    }");
            sb.AppendLine("}");

            context.AddSource($"TypedRegex.generated.cs", sb.ToString());

        }
    }
}
