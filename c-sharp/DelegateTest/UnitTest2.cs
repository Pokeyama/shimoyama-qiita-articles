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

        
        [Fact(DisplayName = "そのまま使う")]
        public void DelegateOnceTest()
        {
            SayHello("Alice");
            SayGoodbye("Bob");
        }
    }
}