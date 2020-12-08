using System;
using System.Text.RegularExpressions.Typed;

namespace TestLib
{
    [TypedRegex(@"(?<Value>\d+) (?<Unit>[a-z]+)")]
    public partial record Amount(int Value, string Unit);
    [TypedRegex(@"(?<X>\d+) (?<Y>\d+) (?<Z>\d+\.\d{2}) (?<G>[\da-zA-Z-]+)", "", "")]
    public partial record MyRecord(int X, int Y, decimal Z, Guid G);

    [TypedRegex(@"")]
    public partial record TestRecordWithoutProperties();

    [TypedRegex(@"")]
    public partial record TestRecordWithIncompleteRegex(int X)
    {
        public TestRecordWithIncompleteRegex(int x, int y) : this(x) { }
    }
}

namespace Blah
{
    public record SomeThing(int X);
}