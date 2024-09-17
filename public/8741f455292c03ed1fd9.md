---
title: 【C#】Expression.Compile()を安易に使ってはいけない理由と対策
tags:
  - C#
  - .NET
  - パフォーマンス
private: false
updated_at: '2024-09-17T12:07:31+09:00'
id: 8741f455292c03ed1fd9
organization_url_name: null
slide: false
ignorePublish: false
---
# はじめに
C#で動的に複雑な処理を実装したい場合、`Func`や`Expression`を使用することがあります。
特に、式ツリー（Expression Tree）を利用して動的なコードを生成し、実行時にコンパイルして実行する方法は強力ですが、注意が必要です。

例えば、以下のようなコードでループ内で`Expression.Compile()`を呼び出しているときを考えます。
```c#
[Fact(DisplayName = "毎回コンパイル")]
public void CompileEveryTimeTest()
{
    // テスト回数
    const int iterations = 1000;

    for (int i = 0; i < iterations; i++)
    {
        // 式ツリーの定義
        Expression<Func<int, int>> expression = x => x * 2;

        // 毎回コンパイル
        Func<int, int> compiledFunc = expression.Compile();

        // デリゲートの実行
        int result = compiledFunc(5);
        _output.WriteLine(result.ToString());
    }
}
```

ループの度に`x => x * 2`という式を`expression.Compile()`しています。
この方法が問題となる理由を解説していきます。

ここからは以下のユニットテストクラスを使って処理の全体時間とCPU使用時間を計測していきます。
```c#
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

    // 以下にテストを並べていきます
    // [Fact]
}
```

# 問題点
## 遅い、重いの二重苦
`Expression`は高度な抽象化を提供し、動的に処理を組み立てることができます。
しかし、式ツリーを実行するためには、一度コンピューターが理解できる形に**コンパイル**して **IL（中間言語）** に変換する必要があります。

上記のコードでは、以下の部分で毎回コンパイルが行われます。
```c#
Func<int, int> compiledFunc = expression.Compile();
```

`dotnet build`をしたときを想像するとわかりやすいのですが、こういった静的言語のビルドは非常に時間がかかります。
厳密には全く違いますが、このビルドプロセスに似た高負荷な処理が毎回実行時に行われていると考えると、その影響の大きさが理解できるでしょう。

## 問題が顕在化しづらい
遅いとはいえ、単純なユニットテストや少ない回数のループではパフォーマンスの問題に気づきにくいです。
例えば、以下のコードを実行して計測すると、次のような結果になります。

```c#
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
```

```
[毎回コンパイル] 実行時間: 67 ms, CPU時間: 72.0822 ms
[一度だけコンパイル] 実行時間: 0 ms, CPU時間: 0.4572 ms
```

1秒にも満たないため、小規模なテストでは問題が表面化しません。
しかし、実際のアプリケーションでは、この処理が頻繁に呼び出されるとパフォーマンスに大きな影響を及ぼします。
最悪の場合、リリース後に問題が発覚することもあります。

そもそもNewRelicのようなAPM監視ツールを導入していないと「ここがネック」というのすらわからないといったことも十分に考えられます。

## VMのコストがかかる
実際のアプリケーションは何回もこの処理が呼ばれることになります。
1回辺りのCPU時間が増大していることから、この処理をさばくためには、サーバーのスペックを向上させるか、サーバーをスケールアウトしなければなりません。
これは直接的にコスト増加につながります。

# 改善策
## Expressionを使わずにFuncを直接使用する
`Expression`を使用する主な理由は、式の構造を解析したり変更したりするためです。
単純にデリゲートとして実行するだけであれば、`Func`を直接使用するだけで事足ります。
```c#
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
```

```
[直接デリゲートを使用] 実行時間: 0 ms, CPU時間: 0.0071 ms
```

どうしてもExpressionを使用しなければいけないとき（式の中身を取り出したいときなど）始めて以下を検討しましょう。
## 一度だけコンパイルしてデリゲートを再利用する
すでに問題点で書いていますが、式が決まっているのならば一回だけコンパイルするようにするだけで解決です。
```c#
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
```

```
[一度だけコンパイル] 実行時間: 0 ms, CPU時間: 0.4572 ms
```

## キャッシュの実装
複数の異なる式を動的にコンパイルする場合、結果をキャッシュすることでコンパイルのオーバーヘッドを削減できます。
この際、キャッシュするコレクションは必ず`ConcurrentDictionary`のようなスレッドセーフなものを使用しましょう。
書き込む可能性のある静的フィールドにスレッドセーフでないコレクションを使用すると、予期せぬ不具合が発生する可能性がありますので注意が必要です。
```c#
// 式をキャッシュしておく
private static readonly ConcurrentDictionary<string, Delegate> _cache = new();

[Fact(DisplayName = "コンパイル結果をキャッシュ")]
public void CachedCompileTest()
{
    MeasurePerformance("コンパイル結果をキャッシュ", () =>
    {
        const int iterations = 1000;

        for (int i = 0; i < iterations; i++)
        {
            // キャッシュキー（式の文字列表現などを使用）
            string key = "x => x * 2";

            // デリゲートの取得または追加
            var compiledFunc = (Func<int, int>)_cache.GetOrAdd(key, _ =>
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
```

```
[コンパイル結果をキャッシュ] 実行時間: 0 ms, CPU時間: 0.011 ms
```

注意点があり、式の文字列表現をキーにすると、異なる式でも同じ文字列になる可能性があります。
式の構造を解析してユニークなキーを生成するか、`Expression` オブジェクトのハッシュコードを使用するなどの方法を検討してください。

## 静的フィールドを使用して事前にコンパイルする
式は複数あるが、ある程度決まっている（規則性がある）場合は事前にstaticで溜め込んでおくのも有効です。
helperのような感じがわかりやすいと思います。

```c#
// 静的フィールドとしてコンパイル済みデリゲートを定義 式の分これを増やしていく
private static readonly Func<int, int> CompiledTwoFunc = CompileTwoExpression();
// private static readonly Func<int, int> CompiledTenFunc = CompileTenExpression();
private static Func<int, int> CompileTwoExpression()
{
    // 式ツリーの定義
    Expression<Func<int, int>> expression = x => x * 2;
    // デリゲートのコンパイル
    return expression.Compile();
}

// private static Func<int, int> CompileTenExpression()
// {
//     // 式ツリーの定義
//     Expression<Func<int, int>> expression = x => x * 10;
//     // デリゲートのコンパイル
//     return expression.Compile();
// }

[Fact(DisplayName = "静的フィールドでコンパイル")]
public void StaticCompiledTest()
{
    MeasurePerformance("静的フィールドでコンパイル", () =>
    {
        const int iterations = 1000;

        for (int i = 0; i < iterations; i++)
        {
            // デリゲートの実行
            int result = CompiledTwoFunc(5);
        }
    });
}
```

```
[静的フィールドでコンパイル] 実行時間: 0 ms, CPU時間: 0.0143 ms
```

静的フィールドを使用することで、コンパイル処理をアプリケーションの初期化時に一度だけ実行します。
これにより、ランタイムでのコンパイルオーバーヘッドを完全に排除し、実行時のパフォーマンスを最大化できます。
また、コンパイルエラーがあればアプリケーションの起動時に検出できるため、信頼性も向上します。

# まとめ
`Expression`を使用した処理は書いたときは達成感を得られる一方で、このようなパフォーマンス上の落とし穴が存在します。

対策としては以下が挙げられます。

1. **Expression を使わずに Func を直接使用する**：可能であれば、これが最も簡単で効率的です。
2. **一度だけコンパイルしてデリゲートを再利用する**：コンパイルのオーバーヘッドを削減できます。
3. **キャッシュを実装する**：動的な式でも、一度コンパイルしたデリゲートを再利用できます。
4. **静的フィールドを使用して事前にコンパイルする**：アプリケーションの初期化時にコンパイルすることで、ランタイムの負荷を軽減します。

どうしても使用しなきゃいけない場面は存在するのですが、その場合も慎重に検討すべきです。

# 参考
https://docs.microsoft.com/ja-jp/dotnet/csharp/expression-trees

https://zenn.dev/higty/articles/73b6b4a402b94e
