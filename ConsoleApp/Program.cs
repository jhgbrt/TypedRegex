using System;
using System.Text.RegularExpressions.Typed;


namespace ConsoleApp
{
    record SomeThing(int X);
    record Amount(int Value, string Unit);
    record MyRecord(int X, int Y);

    class Program
    {
        static TypedRegex<Amount> _sregex1 = TypedRegex.Amount("");
        static TypedRegex<MyRecord> _sregex2 = TypedRegex.MyRecord("");
        static void Main(string[] args)
        {
            TypedRegex<Amount> regex = TypedRegex.Amount(@"(?<Value>\d+) (?<Unit>[a-z]+)"); 
            var amount = regex.Match("5 cm");
            Console.WriteLine(amount);

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
