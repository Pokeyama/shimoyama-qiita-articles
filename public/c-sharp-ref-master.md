---
title: 【C#】参照渡しは副作用を理解した上で使用する【ref/out/修飾子なし】
tags:
  - C#
  - 参照渡し
private: false
updated_at: '2025-04-28T11:10:24+09:00'
id: 2c8facc210743db60914
organization_url_name: null
slide: false
ignorePublish: false
---
# はじめに
参照渡し自体は何も悪くなく、**副作用を理解して使っているか**という観点の記事になります。
現に`Try〇〇`は`out`使われていますし。(慣習的なやつだと思ってます)
題名にC#とつけていますが、副作用に対する考え方は他言語でも同じです。

本記事の`副作用`とは「関数の引数に対する破壊的変更」のことを指しています。

https://ja.wikipedia.org/wiki/%E5%89%AF%E4%BD%9C%E7%94%A8_%28%E3%83%97%E3%83%AD%E3%82%B0%E3%83%A9%E3%83%A0%29#:~:text=%E7%A0%B4%E5%A3%8A%E7%9A%84%E4%BB%A3%E5%85%A5%E3%80%81-,%E5%8F%82%E7%85%A7%E6%B8%A1%E3%81%97,-%E3%81%95%E3%82%8C%E3%81%9F

簡単なコードだと顕在化しづらいのですが、複雑なロジックを持つ処理だと副作用がクリティカルに影響してくるのでまとめます。

# 前提: refありなしの挙動の違い
参照型オブジェクトを引数に取る場合でも、ref指定の有無で動作が大きく変わります。

```c#
class Person { public string Name; }

// refなし: 参照のコピー
void WithoutRef(Person p)
{
    // プロパティ変更は呼び出し元にも反映される
    p.Name = "Bob";
    
    // 変数 p の再代入はローカルのみ。有効範囲はメソッド内部だけ
    p = new Person { Name = "Carol" };
}

// refあり: 変数そのものを渡す
void WithRef(ref Person p)
{
    // プロパティ変更は呼び出し元にも反映
    p.Name = "Bob";
    
    // 変数 p に別インスタンスを再代入すると、呼び出し元の変数も更新される
    p = new Person { Name = "Carol" };
}

var alice = new Person { Name = "Alice" };
WithoutRef(alice);
Console.WriteLine(alice.Name);  // Bob

WithRef(ref alice);
Console.WriteLine(alice.Name);  // Carol
```
`WithoutRef`: p.Name の変更のみ呼び出し先に伝わり、変数自体の再代入はローカルに留まります。

`WithRef`: メソッド内部での再代入も呼び出し元へ伝搬し、変数そのものを書き換えられます。

# 副作用
以下から具体的に副作用を列挙していきます。

## 可読性・予測可能性の低下
呼び出し元の変数がメソッド内で書き換わるため、どこで何が変更されたか把握しづらくなります。例えば以下のように、ref を使うと値が思わぬタイミングで変わるケースがあります。
```c#
class DataPacket
{
    public List<int> Numbers = new List<int>();
    public string Status = "New";
}

void ProcessPacket(ref DataPacket packet)
{
    // (1) 内部リストを mutate
    packet.Numbers.Add(100);
    
    // (2) 条件によっては新しいインスタンスに置き換え
    if (packet.Numbers.Count > 3)
    {
        packet = new DataPacket
        {
            Numbers = new List<int> { -1 },
            Status = "OverflowReset"
        };
    }
    else
    {
        packet.Status = "Processed";
    }
}

// 呼び出し元
var pkt = new DataPacket();
pkt.Numbers.AddRange(new[] { 1, 2, 3 });
ProcessPacket(ref pkt);
// ① 内部リストに 100 が追加される
//    → Numbers = [1,2,3,100]
// ② Numbers.Count == 4 → 新しい DataPacket に置き換わる
//    → pkt.Numbers = [-1], pkt.Status = "OverflowReset"

```
すごい極端ですが、**このように記述できてしまうのが問題**だと思ってます。
上記のように、メソッド呼び出しだけで変数の値が変化する副作用は、コードを追うだけでは見落としやすいです。

### なぜ見通しが悪くなるか
1. 内部状態の変更と参照そのものの書き換えが混在
・`packet.Numbers.Add(100)` と `packet = new DataPacket(...)` の２種類の「変わるロジック」が同一メソッド内にあるため、呼び出し元の変数 `pkt` がどのタイミングでどう変わるか、ソースを追わないと判別しづらい。
2. インスタンスごと置き換わる**可能性**がある
・ 中身を書き換えるだけか、インスタンスごと置き換わるかは全く別の事象であり、その状態で関数から返却される可能性があるのは不自然です。

## テスト・デバッグの複雑化
副作用を伴う設計では、呼び出し前後の状態検証が増え、テストコードやデバッグログが冗長になります。以下のoutパラメータを使ったテスト例を見てみましょう。

```c#
bool TryGetUser(int id, out User user)
{
    // データ取得ロジック...
}

// テストコード
[Test]
public void TestTryGetUser()
{
    bool result = TryGetUser(1, out User user);
    Assert.IsTrue(result);
    Assert.AreEqual("Alice", user.Name);
}
```

引き続き、戻り値で返す設計にするとテストはシンプルになります。

```c#
User GetUser(int id)
{
    // ユーザーが見つからなければ例外をスロー
}

[Test]
public void TestGetUser()
{
    User user = GetUser(1);
    Assert.AreEqual("Alice", user.Name);
}
```

## API設計の一貫性が乱れる
`ref/out`を多用すると、呼び出し側で`ref/out`キーワードを明示しなければならず、シグネチャが複雑化します。

```c#
void ProcessData(ref Data input, out Result output) {
   /* ... */ 
}

```
書き手が参照渡しの副作用を理解した上で書いている人間だけの場合は問題ありません。
しかし、現実そのような現場の方が珍しく（諸説あり）、このようなシステムでは、APIの利用者は各メソッドの副作用を逐一把握しなければなりません。
**関数内ではオブジェクトの書き換えは起こしてはいけません**。と規約を作るだけで、この手間はなくなります。

## 非同期や並列処理との相性の悪化
ref/outは同期的な文脈でのみ有効であり、Taskベースの非同期メソッドや並列LINQといったモダンなAPI設計とは相性が良くありません。
非同期メソッドにrefパラメータを渡せず、設計上の制約を強いられます。

```c#
class Packet { public List<int> Numbers = new(); }

// NG: Parallel.ForEach 内で ref 経由の共有リストを変更すると競合状態に
void ParallelMutate(ref Packet packet)
{
    packet.Numbers = Enumerable.Range(1, 5).ToList();
    Parallel.ForEach(packet.Numbers, n =>
    {
        // 複数スレッドが同じリストに Add する
        packet.Numbers.Add(n * 10);
    });
    // 実行結果は不定。例外が出ることもあるし、要素数が合わなくなることも
}
```
参照渡ししたオブジェクトを並列に書き換えると、スレッドセーフではなくなっています。

# 代替手段
そもそも代替という考え方自体が悪いような気がしますが、こういう考え方もあるよといった感じで。

## in引数を使用する

`in` 修飾子を使うと、**読み取り専用の参照渡し**が可能になります。
大きな構造体（`struct`）をコピーせずに渡しつつ、メソッド内での不意の変更をコンパイル時に防げるため、副作用のリスクを下げつつパフォーマンスも確保できます。

```csharp
// 大きなデータを持つ struct の例
public struct BigData
{
    public readonly int[] Values;
    public BigData(int[] values) => Values = values;

    // 読み取り専用のプロパティやメソッドを定義しておく
    public int Sum() => Values.Sum();
}

// 読み取り専用の参照渡し
void ProcessData(in BigData data)
{
    // OK: メソッド内での読み取り
    Console.WriteLine($"合計: {data.Sum()}");

    // NG: in 引数は読み取り専用のためコンパイルエラー
    // data.Values = new int[] { 1, 2, 3 };
    // data = new BigData(new int[] { });
}
```

### in句の注意点
オブジェクトのフィールドの書き換えは許容されるので注意が必要です。（非イミュータブル）
```c#
class User{ public int id; }

private void SetId(in User user)
{
    user.id = 999;
}

[Fact]
public void InTest()
{
    var user = new User{ id = 1 };
    SetId(in user);
    _output.WriteLine(user.id.ToString()); // 999
}
```

フィールドの書き換えまで防ぎたい場合はプロパティに`init`をつけてあげましょう。

```c#
class User{ public int id{ get; init; } }

private void SetId(in User user)
{
    user.id = 999; // ここでコンパイルエラーになる
}

[Fact]
public void InTest()
{
    var user = new User{ id = 1 };
    SetId(in user);
    _output.WriteLine(user.id.ToString());
}
```

## DTO にまとめて返す
複数の戻り値をまとめたい場合は、専用のDTOクラスや構造体を定義して返します。
```c#
class ProcessResult {
    public bool Success { get; init; }
    public Data Transformed { get; init; }
    public string Message { get; init; }
}

ProcessResult ProcessData(Data input) {
    // ロジック...
    return new ProcessResult { Success = true, Transformed = result, Message = "OK" };
}
```
この場合、**DTOクラスは引数として使わない**というルールを設ければ、副作用が抑制できます。

## record 型を使ったイミュータブルな戻り値

`record` を使うことで、**データコンテナをイミュータブル**に定義しつつ、複数の情報をまとめて返却できます。

```csharp
// 入力データも record として定義
public record Data(int X, int Y);

// 処理結果用の record
public record ProcessResult(bool IsSuccess, Data Transformed, string Message);

ProcessResult ProcessData(Data input)
{
    // with 式で新しいインスタンスを生成（イミュータブル）
    var transformed = input with { X = input.X + 1 };

    // 必要に応じてさらに加工
    return new ProcessResult(
        IsSuccess: true,
        Transformed: transformed,
        Message: "処理が完了しました"
    );
}

// 呼び出し側
var original = new Data(1, 2);
var result   = ProcessData(original);
Console.WriteLine($"Success: {result.IsSuccess}, X: {result.Transformed.X}");
```

この書き方のいいところはイミュータブルな状態を強制できるので、引数で持っているオブジェクトの書き換えを万に一つも考慮しなくてよくなる点です。

# 参照渡しを許容するケース

1. **低レイヤーの高パフォーマンス処理**  
   カツカツにメモリも節約したいときは参照渡しは真っ先に考慮にあげるべきだと思います。 
2. **TryParse 系の慣例**  
   `int.TryParse(string, out int result)` のように慣例的に元のライブラリでも使用されているパターンは周知してしかるべきなので、問題ないと思います。

# まとめ
過去に何も理解せず参照渡しばかりのコードを書いてしまって地獄を見たので、地獄を言語化してみました。
難しいことを考えたくない場合、チームのコーディング規約として「引数は書き換えない」ルールを定めるのが重要だと思います。  

