# gc-benchmark

C# と Go のガベージコレクション(GC)の挙動を、実際に動かして観察するためのデモコードです。

Qiita 記事「[【C# / Go】GC の仕組みを実測で理解する（世代別 vs 非世代）](../public/gc.md)」の計測に使用しています。

## 観察している内容

### C# (`csharp/`)

- **Demo1**: アロケーション量と GC 回数の関係（`GC.CollectionCount`）
- **Demo2**: 生き残ったオブジェクトの世代昇格（`GC.GetGeneration`）
- **Demo3**: Large Object Heap (LOH) のしきい値 85,000 byte（`GC.GetGeneration`）
- **Demo4**: GC 停止時間（`GC.GetTotalPauseDuration`）

```bash
cd csharp
dotnet run -c Release
```

### Go (`go/`)

- **Demo1**: アロケーション量と GC 回数・停止時間（`runtime.MemStats` の `NumGC` / `PauseTotalNs`）
- **Demo2**: `GOGC` を変えたときの GC 回数・停止時間のトレードオフ（`debug.SetGCPercent`）

```bash
cd go
go run .

# GC のたびに1行ログを出す（実際の GC トレースを見る）
GODEBUG=gctrace=1 go run .
```

## 注意

- 計測値はマシン・実行時の負荷に左右されます。絶対値ではなく傾向（GC 回数の桁、増減の方向）を見てください。
- C# は **Server GC + Background GC**（ASP.NET Core などの既定構成）で観察しています（csproj の `ServerGarbageCollection` / `ConcurrentGarbageCollection` で設定）。Workstation GC に切り替えると GC 回数などの挙動が変わります（例: 環境変数 `DOTNET_gcServer=0 DOTNET_gcConcurrent=0` で実行）。
