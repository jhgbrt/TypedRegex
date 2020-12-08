using System;
using System.Text.RegularExpressions;

namespace System.Text.RegularExpressions.Typed
{
    internal class TypedRegexAttribute : Attribute
    {
        public Regex Regex { get; }
        public string[] Formats { get; }
        public TypedRegexAttribute(string pattern, params string[] formats)
        {
            Regex = new Regex(pattern);
            Formats = formats;
        }
    }
}
