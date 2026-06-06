using System.Diagnostics;

// 要素数。ツイートの10,000では一瞬すぎて差が見えないので、差が観測できる規模まで増やす
const int N = 1_000_000;
// 1回だけだとJITやキャッシュの影響でブレるので、複数回の平均をとる
const int Runs = 7;

Console.WriteLine($"== C# (.NET {Environment.Version}) ==");
Console.WriteLine($"N = {N:N0} / Runs = {Runs}（平均値）");
Console.WriteLine();

// ウォームアップ（JIT コンパイルを先に済ませておく）
BuildDictNoCap(); BuildDictCap(); BuildListNoCap(); BuildListCap();

Console.WriteLine("[Dictionary<string, object>]");
Measure("容量指定なし  new Dictionary<>()", BuildDictNoCap);
Measure("容量指定あり  new Dictionary<>(N)", BuildDictCap);
Console.WriteLine();
Console.WriteLine("[List<int>]");
Measure("容量指定なし  new List<int>()", BuildListNoCap);
Measure("容量指定あり  new List<int>(N)", BuildListCap);

// ---- 計測本体 ----
// build()をRuns回実行し、実行時間と「割り当てられたバイト数」の平均を出す。
// GC.GetAllocatedBytesForCurrentThread()は、途中のリサイズで捨てられた配列も含めた
// 累計アロケーション量なので、容量指定による無駄な再確保の差がそのまま出る。
static void Measure(string label, Func<object> build)
{
    double totalMs = 0;
    double totalAlloc = 0;
    for (int r = 0; r < Runs; r++)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long before = GC.GetAllocatedBytesForCurrentThread();
        var sw = Stopwatch.StartNew();
        object o = build();
        sw.Stop();
        long after = GC.GetAllocatedBytesForCurrentThread();
        GC.KeepAlive(o); // 最適化で消されないように参照を保持

        totalMs += sw.Elapsed.TotalMilliseconds;
        totalAlloc += after - before;
    }

    double ms = totalMs / Runs;
    double mb = totalAlloc / Runs / 1024 / 1024;
    Console.WriteLine($"  {label,-34} time = {ms,8:F1} ms   alloc = {mb,8:F1} MB");
}

// ---- 計測対象 ----
// ツイートと同じく「stringキー + object値」を1,000,000件入れる
static object BuildDictNoCap()
{
    var map = new Dictionary<string, object>();
    for (int i = 0; i < N; i++) map.Add("key" + i, new object());
    return map;
}

static object BuildDictCap()
{
    // 入れる件数が分かっているので最初から確保しておく
    var map = new Dictionary<string, object>(N);
    for (int i = 0; i < N; i++) map.Add("key" + i, new object());
    return map;
}

// 普段よく使う可変長配列（List）でも同じことを検証する
static object BuildListNoCap()
{
    var list = new List<int>();
    for (int i = 0; i < N; i++) list.Add(i);
    return list;
}

static object BuildListCap()
{
    var list = new List<int>(N);
    for (int i = 0; i < N; i++) list.Add(i);
    return list;
}
