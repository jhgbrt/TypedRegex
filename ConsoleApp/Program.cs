using System;
using System.Text.RegularExpressions;
using System.Text.RegularExpressions.Typed;

using TestLib;

/*
 * With this approach, it's required to mark records as partial
 */

namespace TestLib
{
    

    [TypedRegex(@"(?<Value>\d+) (?<Uit>[a-z]+)")]
    public partial record Amount(int Value, string Unit);
    [TypedRegex(@"(?<X>\d+) (?<Y>\d+) (?<Z>\d+\.\d{2}) (?<G>[\da-zA-Z-]+)")]
    public partial record MyRecord(int X, int Y, decimal Z, Guid G);

    [TypedRegex(@"")]
    public partial record TestRecordWithoutProperties();
    
    [TypedRegex(@"")]
    public partial record TestRecordWithIncompleteRegex(int X)
    {
        public TestRecordWithIncompleteRegex(int x, int y) : this(x) { }
    }
}

namespace ConsoleApp
{
    class Program
    {

        //static readonly TypedRegex<MyRecord> _myrecord = TypedRegex.MyRecord(new Regex(@"(?<X>\d+) (?<Y>\d+) (?<Z>\d+\.\d{2}) (?<G>[\da-zA-Z-]+)").ToString());

        static void Main(string[] args)
        {
            var amount = Amount.Parse("5 cm");
            Console.WriteLine(amount);
            var x = Amount.Parse("123 345 1.25 C4EA3B94-50D9-4E6D-AF0D-D2FD072A0DC4");
            Console.WriteLine(x);

            Console.WriteLine("Types in this assembly:");
            foreach (Type t in typeof(Program).Assembly.GetTypes())
            {
                Console.WriteLine(t.FullName);
            }
        }
    }
}
