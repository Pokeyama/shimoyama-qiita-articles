using System;
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

        [Fact]
        public void CompileEveryTimeTest()
        {
            // テスト回数
            const int iterations = 1000;

            // プロセス情報の取得
            var process = Process.GetCurrentProcess();
            var cpuStartTime = process.TotalProcessorTime;
            var stopwatch = Stopwatch.StartNew();

            for (int i = 0; i < iterations; i++)
            {
                // 式ツリーの定義
                Expression<Func<int, int>> expression = x => x * 2;

                // 毎回コンパイル
                Func<int, int> compiledFunc = expression.Compile();

                // デリゲートの実行
                int result = compiledFunc(5);
            }

            stopwatch.Stop();
            var cpuEndTime = process.TotalProcessorTime;

            // 計測結果の取得
            var wallClockTime = stopwatch.ElapsedMilliseconds;
            var cpuTime = (cpuEndTime - cpuStartTime).TotalMilliseconds;

            // 結果の出力
            _output.WriteLine($"[毎回コンパイル] 実行時間: {wallClockTime} ms, CPU時間: {cpuTime} ms");
        }

        [Fact]
        public void CompileOnceTest()
        {
            // テスト回数
            const int iterations = 1000;

            // 式ツリーの定義
            Expression<Func<int, int>> expression = x => x * 2;

            // 一度だけコンパイル
            Func<int, int> compiledFunc = expression.Compile();

            // プロセス情報の取得
            var process = Process.GetCurrentProcess();
            var cpuStartTime = process.TotalProcessorTime;
            var stopwatch = Stopwatch.StartNew();

            for (int i = 0; i < iterations; i++)
            {
                // デリゲートの実行
                int result = compiledFunc(5);
            }

            stopwatch.Stop();
            var cpuEndTime = process.TotalProcessorTime;

            // 計測結果の取得
            var wallClockTime = stopwatch.ElapsedMilliseconds;
            var cpuTime = (cpuEndTime - cpuStartTime).TotalMilliseconds;

            // 結果の出力
            _output.WriteLine($"[一度だけコンパイル] 実行時間: {wallClockTime} ms, CPU時間: {cpuTime} ms");
        }
    }
}
