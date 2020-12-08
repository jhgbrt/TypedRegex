using System.Text.RegularExpressions;

namespace TestLib
{
    public partial record MyRecord
    {
        static readonly Regex _regex = new Regex(@"(?<X>\d+) (?<Y>\d+) (?<Z>\d+\.\d{2}) (?<G>[\da-zA-Z-]+)");
        public static MyRecord Parse(string s)
        {
                var match = _regex.Match(s);
                var x = System.Int32.Parse(match.Groups["X"].Value);
                var y = System.Int32.Parse(match.Groups["Y"].Value);
                var z = System.Decimal.Parse(match.Groups["Z"].Value);
                var g = System.Guid.Parse(match.Groups["G"].Value);
                return new MyRecord(x, y, z, g);

        }
    }
}

