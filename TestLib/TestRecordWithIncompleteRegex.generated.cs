using System.Text.RegularExpressions;

namespace TestLib
{
    public partial record TestRecordWithIncompleteRegex
    {
        static readonly Regex _regex = new Regex(@"");
        public static TestRecordWithIncompleteRegex Parse(string s)
        {
            var match = _regex.Match(s);
            var x = System.Int32.Parse(match.Groups["X"].Value);
            return new TestRecordWithIncompleteRegex(x);
        }
    }
}

