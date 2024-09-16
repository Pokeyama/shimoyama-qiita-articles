using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq.Expressions;
using Xunit;
using Xunit.Abstractions;

namespace ExpressionCompileTest
{
    public class ExpressionCompilePerformanceTests
    {
        private readonly ITestOutputHelper _output;

        public ExpressionCompilePerformanceTests(ITestOutputHelper output)
        {
            _output = output;
        }

        // 共通の計測メソッド
        private void MeasurePerformance(string testName, Action testAction)
        {
            // プロセス情報の取得
            var process = Process.GetCurrentProcess();
            var cpuStartTime = process.TotalProcessorTime;
            var stopwatch = Stopwatch.StartNew();

            // テスト処理の実行
            testAction();

            stopwatch.Stop();
            var cpuEndTime = process.TotalProcessorTime;

            // 計測結果の取得
            var wallClockTime = stopwatch.ElapsedMilliseconds;
            var cpuTime = (cpuEndTime - cpuStartTime).TotalMilliseconds;

            // 結果の出力
            _output.WriteLine($"[{testName}] 実行時間: {wallClockTime} ms, CPU時間: {cpuTime} ms");
        }

        [Fact(DisplayName = "毎回コンパイル")]
        public void CompileEveryTimeTest()
        {
            MeasurePerformance("毎回コンパイル", () =>
            {
                const int iterations = 1000;

                for (int i = 0; i < iterations; i++)
                {
                    // 式ツリーの定義
                    Expression<Func<int, int>> expression = x => x * 2;

                    // 毎回コンパイル
                    Func<int, int> compiledFunc = expression.Compile();

                    // デリゲートの実行
                    int result = compiledFunc(5);
                }
            });
        }

        [Fact(DisplayName = "一度だけコンパイル")]
        public void CompileOnceTest()
        {
            MeasurePerformance("一度だけコンパイル", () =>
            {
                const int iterations = 1000;

                // 式ツリーの定義
                Expression<Func<int, int>> expression = x => x * 2;

                // 一度だけコンパイル
                Func<int, int> compiledFunc = expression.Compile();

                for (int i = 0; i < iterations; i++)
                {
                    // デリゲートの実行
                    int result = compiledFunc(5);
                }
            });
        }

        // 直接デリゲートを定義
        private static readonly Func<int, int> DirectFunc = x => x * 2;

        [Fact(DisplayName = "直接デリゲートを使用")]
        public void DirectDelegateTest()
        {
            MeasurePerformance("直接デリゲートを使用", () =>
            {
                const int iterations = 1000;

                for (int i = 0; i < iterations; i++)
                {
                    // デリゲートの実行
                    int result = DirectFunc(5);
                }
            });
        }

        // 式をキャッシュしておく
        private static readonly ConcurrentDictionary<string, Delegate> _cache = new();

        [Fact(DisplayName = "コンパイル結果をキャッシュ")]
        public void CompileCacheTest()
        {
            MeasurePerformance("コンパイル結果をキャッシュ", () =>
            {
                const int iterations = 1000;

                for (int i = 0; i < iterations; i++)
                {
                    // キャッシュキー（式の文字列表現などを使用）
                    string key = "x => x * 2";

                    // デリゲートの取得または追加
                    var compiledFunc = (Func<int, int>) _cache.GetOrAdd(key, _ =>
                    {
                        // 式ツリーの定義
                        Expression<Func<int, int>> expression = x => x * 2;
                        // デリゲートのコンパイル
                        return expression.Compile();
                    });

                    // デリゲートの実行
                    int result = compiledFunc(5);
                }
            });
        }

        // 静的フィールドとしてコンパイル済みデリゲートを定義
        private static readonly Func<int, int> CompiledFunc = CompileExpression();

        private static Func<int, int> CompileExpression()
        {
            // 式ツリーの定義
            Expression<Func<int, int>> expression = x => x * 2;
            // デリゲートのコンパイル
            return expression.Compile();
        }

        [Fact(DisplayName = "静的フィールドでコンパイル")]
        public void StaticCompiledTest()
        {
            MeasurePerformance("静的フィールドでコンパイル", () =>
            {
                const int iterations = 1000;

                for (int i = 0; i < iterations; i++)
                {
                    // デリゲートの実行
                    int result = CompiledFunc(5);
                }
            });
        }
    }
}