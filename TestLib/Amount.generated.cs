using System.Text.RegularExpressions;

namespace TestLib
{
    public partial record Amount
    {
        static readonly Regex _regex = new Regex(@"(?<Value>\d+) (?<Uit>[a-z]+)");
        public static Amount Parse(string s)
        {
            var match = _regex.Match(s);
            var value = System.Int32.Parse(match.Groups["Value"].Value);
            var unit = match.Groups["Unit"].Value;
            return new Amount(value, unit);
        }
    }
}

