# capacity-benchmark

コレクションの **初期容量（キャパシティ）を指定するかしないか** で、所要時間とメモリ使用量がどれだけ変わるかを C# / Go / PHP で実測するための計測コードです。

Qiita 記事「[【C# / Go / PHP】HashMap/配列の初期容量、指定するとどれだけ速くなるのか実測した](../public/array.md)」の計測に使用しています。

## 計測内容

各言語で以下を `N = 1,000,000` 件、7回平均で計測します。

- **ハッシュマップ系**: `Dictionary` / `map` / 連想配列（ツイート元ネタの `HashMap` 相当）
- **可変長配列系**: `List` / slice / 配列

それぞれ「容量指定なし」と「容量指定あり」を比較します。

## 実行方法

### C# (.NET 10)

```bash
cd csharp
dotnet run -c Release
```

### Go (1.26+)

```bash
cd go
go run .
```

### PHP (8.2+ / SplFixedArray と memory_reset_peak_usage を使用)

```bash
cd php
php -d memory_limit=2G benchmark.php
```

## 注意

- 計測値はマシンや実行時の負荷に左右されるため、**絶対値ではなく「指定あり/なしの比」** を見てください。
- メモリは C# / Go が「累計アロケーション量（途中のリサイズで捨てた分も含む）」、PHP が「ピークメモリ使用量」と指標が異なります。言語間の横比較ではなく、同一言語内の比較に使ってください。
