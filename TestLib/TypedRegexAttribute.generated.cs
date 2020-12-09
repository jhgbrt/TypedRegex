using System;
using System.Text.RegularExpressions;

namespace System.Text.RegularExpressions.Typed
{
    internal class TypedRegexAttribute : Attribute
    {
        public Regex Regex { get; }
        public TypedRegexAttribute(string pattern) => Regex = new Regex(pattern);
    }
}
