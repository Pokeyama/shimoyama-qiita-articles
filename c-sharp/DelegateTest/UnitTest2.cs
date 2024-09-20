using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq.Expressions;
using Xunit;
using Xunit.Abstractions;

namespace ExpressionCompileTest
{
    public class DelegateTests
    {
        private readonly ITestOutputHelper _output;

        public DelegateTests(ITestOutputHelper output)
        {
            _output = output;
        }


        // デリゲートの定義
        public delegate void GreetingDelegate(string name);

        [Fact(DisplayName = "そのまま使う")]
        public void DelegateTest()
        {
            // デリゲートのインスタンスを作成し、メソッドを参照
            GreetingDelegate greeting = new GreetingDelegate(SayHello);

            // デリゲートを使用してメソッドを呼び出す
            greeting("Alice");

            // 別のメソッドをデリゲートに割り当てる
            greeting = SayGoodbye;
            greeting("Bob");
        }

        void SayHello(string name)
        {
            _output.WriteLine($"こんにちは、{name}さん！");
        }

        void SayGoodbye(string name)
        {
            _output.WriteLine($"さようなら、{name}さん！");
        }


        [Fact]
        public void DelegateOnceTest()
        {
            SayHello("Alice");
            SayGoodbye("Bob");
        }

        [Fact(DisplayName = "Actionで書いてみる")]
        public void ActionTest()
        {
            Action<string> greet1 = name => SayHello(name);
            Action<string> greet2 = name => SayGoodbye(name);

            // デリゲートの呼び出し
            greet1("Alice");
            greet1("Bob");
            greet2("Alice");
            greet2("Bob");
        }



        [Fact(DisplayName = "Funcで書いてみる")]
        public void FuncTest()
        {
            Func<string, string> greet1 = name => $"こんにちは、{name}さん！";
            Func<string, string> greet2 = name => FuncSayGoodbye(name);

            // デリゲートの呼び出し
            _output.WriteLine(greet1("Alice"));
            _output.WriteLine(greet1("Bob"));
            _output.WriteLine(greet2("Alice"));
            _output.WriteLine(greet2("Bob"));
        }

        string FuncSayHello(string name)
        {
            return ($"こんにちは、{name}さん！");
        }

        string FuncSayGoodbye(string name)
        {
            return ($"さようなら、{name}さん！");
        }

        [Fact(DisplayName = "Collectionで扱う")]
        public void CollectionTest()
        {
            List<Func<string, string>> funcs = new();
            Func<string, string> greet1 = name => $"こんにちは、{name}さん！";
            funcs.Add(greet1);
            Func<string, string> greet2 = name => FuncSayGoodbye(name);
            funcs.Add(greet2);

            foreach (var g in funcs)
            {
                _output.WriteLine(g("Alice"));
            }
        }

        [Fact(DisplayName = "チェーンして呼び出す")]
        public void DelegateChainTest()
        {
            Action<string> greet1 = name => _output.WriteLine($"こんにちは、{name}さん！");
            greet1 += name => SayGoodbye(name);

            greet1("Alice");
        }


        [Fact(DisplayName = "引数にデリゲートを使う")]
        public void DelegateFuncTest()
        {
            // 文字列のリスト
            var names = new List<string> { "Alice", "Bob", "Jone" };

            ProcessNames(names, SayHello);

            ProcessNames(names, SayGoodbye);
        }

        void ProcessNames(List<string> names, Action<string> process)
        {
            foreach (var name in names)
            {
                process(name);
            }
        }

        [Fact(DisplayName = "デリゲートを使わない場合")]
        public void WithoutDelegateTest()
        {
            var names = new List<string> { "Alice", "Bob", "Jone" };

            foreach (var name in names)
            {
                SayHello(name);
            }

            foreach (var name in names)
            {
                SayGoodbye(name);
            }
        }


        [Theory(DisplayName = "処理を動的に変えたい")]
        [InlineData("Alice")]
        [InlineData("Jone")]
        public void DynamicMethodTest(string name)
        {
            Func<string, string> func;
            if (name == "Alice")
            {
                func = (name) => $"こんにちは、{name}さん！";
            }
            else
            {
                func = (name) => $"さようなら、{name}さん！";
            }

            _output.WriteLine(func(name));
        }

        [Fact(DisplayName = "動的な式の構築")]
        public void DynamicExpressionTest()
        {
            ParameterExpression paramLeft = Expression.Parameter(typeof(int), "a");
            ParameterExpression paramRight = Expression.Parameter(typeof(int), "b");

            // a + b を表す式ツリーを作成
            BinaryExpression body = Expression.Add(paramLeft, paramRight);

            // 式ツリーをLambda式に変換
            Expression<Func<int, int, int>> addExpression = Expression.Lambda<Func<int, int, int>>(body, paramLeft, paramRight);

            // コンパイルしてデリゲートを作成
            Func<int, int, int> addFunc = addExpression.Compile();

            var result = addFunc(1, 2);
            _output.WriteLine($"1 + 2 = {result}");
        }

        [Theory(DisplayName = "四則演算")]
        [InlineData("+")]
        [InlineData("-")]
        [InlineData("*")]
        [InlineData("/")]
        public void ExpressionNumTest(string operation)
        {
            // 動的に式を構築
            Func<int, int, int> func = BuildExpression(operation);

            if (func != null)
            {
                int a = 10;
                int b = 5;
                int result = func(a, b);
                Console.WriteLine($"{a} {operation} {b} = {result}");
            }
            else
            {
                Console.WriteLine("無効な演算子が入力されました。");
            }
        }

        static Func<int, int, int> BuildExpression(string operation)
        {
            // パラメーターの定義 (a, b)
            ParameterExpression paramA = Expression.Parameter(typeof(int), "a");
            ParameterExpression paramB = Expression.Parameter(typeof(int), "b");

            // 演算子に応じた式のボディを定義
            BinaryExpression body = null;

            switch (operation)
            {
                case "+":
                    body = Expression.Add(paramA, paramB);
                    break;
                case "-":
                    body = Expression.Subtract(paramA, paramB);
                    break;
                case "*":
                    body = Expression.Multiply(paramA, paramB);
                    break;
                case "/":
                    body = Expression.Divide(paramA, paramB);
                    break;
                default:
                    return null;
            }

            // ラムダ式を構築
            var expression = Expression.Lambda<Func<int, int, int>>(body, paramA, paramB);

            // コンパイルしてデリゲートを生成
            return expression.Compile();
        }

        public static void ValidateNotNull<T>(T obj, Expression<Func<T, object>> expression)
        {
            var propertyName = GetPropertyName(expression);
            var compiledExpression = expression.Compile();
            var value = compiledExpression(obj);

            if (value == null)
            {
                throw new ArgumentNullException(propertyName, $"{propertyName} は null であってはなりません。");
            }
        }

        // プロパティ名を取得するメソッド
        public static string GetPropertyName<T>(Expression<Func<T, object>> expression)
        {
            MemberExpression memberExpression;

            // 式のボディが UnaryExpression（ボクシングされている場合）
            if (expression.Body is UnaryExpression unaryExpression && unaryExpression.Operand is MemberExpression member)
            {
                memberExpression = member;
            }
            // 式のボディが MemberExpression
            else if (expression.Body is MemberExpression memberExp)
            {
                memberExpression = memberExp;
            }
            else
            {
                throw new InvalidOperationException("無効な式です。");
            }

            return memberExpression.Member.Name;
        }
    }
}