namespace Calc;

// CIが動くことを確認するためだけの最小クラス
public static class Calculator
{
    public static int Add(int a, int b) => a + b;

    public static int Multiply(int a, int b) => a * b;

    // 0除算はDivideByZeroExceptionをそのまま投げる
    public static int Divide(int a, int b) => a / b;
}
