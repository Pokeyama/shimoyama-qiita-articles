using Calc;
using Xunit;

namespace Calc.Tests;

public class CalculatorTests
{
    [Theory]
    [InlineData(1, 2, 3)]
    [InlineData(-1, 1, 0)]
    [InlineData(0, 0, 0)]
    public void Add_足し算の結果が返る(int a, int b, int expected)
    {
        Assert.Equal(expected, Calculator.Add(a, b));
    }

    [Fact]
    public void Multiply_掛け算の結果が返る()
    {
        Assert.Equal(12, Calculator.Multiply(3, 4));
    }

    [Fact]
    public void Divide_ゼロ除算は例外になる()
    {
        Assert.Throws<DivideByZeroException>(() => Calculator.Divide(1, 0));
    }
}
