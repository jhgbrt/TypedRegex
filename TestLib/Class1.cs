using System;

namespace TestLib
{
    public record Amount(int Value, string Unit);
    public record MyRecord(int X, int Y, decimal Z, Guid G);
}

namespace Blah
{
    public record SomeThing(int X);
}