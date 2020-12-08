using System;
using System.Linq;
using System.Globalization;
using System.Text.RegularExpressions;
using TestLib;
using Blah;

namespace System.Text.RegularExpressions.Typed
{
    internal class TypedRegexAttribute : Attribute
    {
        public Regex Regex { get; }
        public TypedRegexAttribute(string pattern) => Regex = new Regex(pattern);
    }

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
                throw new FormatException($"The regex does not contain capture groups for properties {string.Join(",", missingCaptureGroups)}. The regular expression contained the following groups: '{string.Join(",", groupNames)}'");
            }
            _regex = regex;
        }
    }

    internal class TypedRegex
    {

        internal static TypedRegex<Amount> Amount(string pattern) => new AmountTypedRegex(pattern);
        
        class AmountTypedRegex : TypedRegex<Amount>
        {
            public AmountTypedRegex(string pattern) : base(pattern) { }
            
            protected override string[] PropertyNames => new[] { "Unit", "Value" };

            internal override Amount Match(string input)
            {
                var match = _regex.Match(input);
                var value = System.Int32.Parse(match.Groups["Value"].Value);
                var unit = match.Groups["Unit"].Value;
                return new Amount(value, unit);

            }
        }

        internal static TypedRegex<MyRecord> MyRecord(string pattern) => new MyRecordTypedRegex(pattern);
        
        class MyRecordTypedRegex : TypedRegex<MyRecord>
        {
            public MyRecordTypedRegex(string pattern) : base(pattern) { }
            
            protected override string[] PropertyNames => new[] { "G", "X", "Y", "Z" };

            internal override MyRecord Match(string input)
            {
                var match = _regex.Match(input);
                var x = System.Int32.Parse(match.Groups["X"].Value);
                var y = System.Int32.Parse(match.Groups["Y"].Value);
                var z = System.Decimal.Parse(match.Groups["Z"].Value);
                var g = System.Guid.Parse(match.Groups["G"].Value);
                return new MyRecord(x, y, z, g);

            }
        }

        internal static TypedRegex<SomeThing> SomeThing(string pattern) => new SomeThingTypedRegex(pattern);
        
        class SomeThingTypedRegex : TypedRegex<SomeThing>
        {
            public SomeThingTypedRegex(string pattern) : base(pattern) { }
            
            protected override string[] PropertyNames => new[] { "X" };

            internal override SomeThing Match(string input)
            {
                var match = _regex.Match(input);
                var x = System.Int32.Parse(match.Groups["X"].Value);
                return new SomeThing(x);

            }
        }
    }
}

