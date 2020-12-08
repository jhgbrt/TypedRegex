using System.Text.RegularExpressions;

namespace TestLib
{
    public partial record TestRecordWithoutProperties
    {
        static readonly Regex _regex = new Regex(@"");
        public static TestRecordWithoutProperties Parse(string s)
        {
                var match = _regex.Match(s);
                return new TestRecordWithoutProperties();

        }
    }
}

