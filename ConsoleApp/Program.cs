using System;
using System.Text.RegularExpressions;
using System.Text.RegularExpressions.Typed;

using TestLib;

namespace TestLib
{
    public record Amount(int Value, string Unit);
    public record MyRecord(int X, int Y, decimal Z, Guid G);
}

namespace Blah
{
    record SomeThing(int X);
}

namespace ConsoleApp
{

    class Program
    {

        static readonly TypedRegex<MyRecord> _myrecord = TypedRegex.MyRecord(new Regex(@"(?<X>\d+) (?<Y>\d+) (?<Z>\d+\.\d{2}) (?<G>[\da-zA-Z-]+)").ToString());

        static void Main(string[] args)
        {
            TypedRegex<Amount> regex = TypedRegex.Amount(@"(?<Value>\d+) (?<Unit>[a-z]+)"); 
            var amount = regex.Match("5 cm");
            Console.WriteLine(amount);
            var x = _myrecord.Match("123 345 1.25 C4EA3B94-50D9-4E6D-AF0D-D2FD072A0DC4");
            TypedRegex.SomeThing("");

            Console.WriteLine(x);

            try
            {
                var invalid = TypedRegex.Amount(@"(?<Vlue>\d+) (?<Uni>[a-z]+)");
                var anotheramount = invalid.Match("5 cm");
                Console.WriteLine(anotheramount);
            }
            catch (FormatException e)
            {
                Console.WriteLine(e.Message);
            }

            Console.WriteLine("Types in this assembly:");
            foreach (Type t in typeof(Program).Assembly.GetTypes())
            {
                Console.WriteLine(t.FullName);
            }
        }
    }
}
