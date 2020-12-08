using System;
using System.Linq;
using System.Globalization;
using System.Text.RegularExpressions;
using TestLib;

namespace TestLib
{
    public partial record TestRecord
    {
        static readonly Regex _regex = new Regex(@"");
        public static TestRecord Parse(string s)
        {
                var match = _regex.Match(s);
                return new TestRecord();

        }
    }
}

