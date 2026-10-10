using System.Runtime;

internal static class Program
{
    // static フィールドへ代入することでエスケープを強制し、ヒープ確保を消されないようにする
    // （ローカル変数のままだと JIT がアロケーションごと削除してしまう）
    private static byte[]? Blackhole;

    private static void Main()
    {
        Console.WriteLine($"== C# GC demo (.NET {Environment.Version}) ==");
        Console.WriteLine($"GC mode = {(GCSettings.IsServerGC ? "Server" : "Workstation")}, LatencyMode = {GCSettings.LatencyMode}");
        Console.WriteLine();

        Demo1_AllocationPressure();
        Demo2_Promotion();
        Demo3_LOH();
        Demo4_PauseTime();
    }

    // ------------------------------------------------------------------------
    // Demo1: アロケーション量が GC 回数を決める（世代別 GC の核心）
    // 同じ仕事量でも「毎回 new する」か「使い回す」かで Gen0 GC の回数が激変する。
    // ------------------------------------------------------------------------
    private static void Demo1_AllocationPressure()
    {
        Console.WriteLine("[Demo1] アロケーション圧 -> GC 回数");
        const int N = 10_000_000;

        // (A) 毎回 new byte[64]（短命オブジェクトを大量生産）
        var a = Run(() =>
        {
            for (int i = 0; i < N; i++)
            {
                Blackhole = new byte[64];
                Blackhole[0] = (byte)i;
            }
        });
        Console.WriteLine($"  毎回 new byte[64] : Gen0={a.gen0,5}  Gen1={a.gen1,4}  Gen2={a.gen2,3}  alloc={a.allocMb,6} MB");

        // (B) 1個を使い回す（アロケーションなし）
        var b = Run(() =>
        {
            var buf = new byte[64];
            for (int i = 0; i < N; i++)
            {
                buf[0] = (byte)i;
            }
            Blackhole = buf;
        });
        Console.WriteLine($"  1個を使い回し     : Gen0={b.gen0,5}  Gen1={b.gen1,4}  Gen2={b.gen2,3}  alloc={b.allocMb,6} MB");
        Console.WriteLine();
    }

    // GC 回数とアロケーション量(MB)の差分をとって返す
    private static (int gen0, int gen1, int gen2, long allocMb) Run(Action body)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        int g0 = GC.CollectionCount(0), g1 = GC.CollectionCount(1), g2 = GC.CollectionCount(2);
        long alloc = GC.GetTotalAllocatedBytes();
        body();
        return (GC.CollectionCount(0) - g0,
                GC.CollectionCount(1) - g1,
                GC.CollectionCount(2) - g2,
                (GC.GetTotalAllocatedBytes() - alloc) / 1024 / 1024);
    }

    // ------------------------------------------------------------------------
    // Demo2: 生き残ったオブジェクトは世代が昇格する（世代仮説の実演）
    // ------------------------------------------------------------------------
    private static void Demo2_Promotion()
    {
        Console.WriteLine("[Demo2] 生き残ったオブジェクトの世代昇格");
        var obj = new byte[100];
        Console.WriteLine($"  生成直後                : Gen{GC.GetGeneration(obj)}");
        GC.Collect(0);
        Console.WriteLine($"  Gen0 GC を1回生き延びた : Gen{GC.GetGeneration(obj)}");
        GC.Collect(1);
        Console.WriteLine($"  Gen1 GC を1回生き延びた : Gen{GC.GetGeneration(obj)}");
        GC.KeepAlive(obj);
        Console.WriteLine();
    }

    // ------------------------------------------------------------------------
    // Demo3: 85,000 byte 以上の配列は Large Object Heap (LOH) 行きで Gen2 扱い
    // ------------------------------------------------------------------------
    private static void Demo3_LOH()
    {
        Console.WriteLine("[Demo3] Large Object Heap のしきい値 (85,000 byte)");
        var small = new byte[84_000];
        var large = new byte[85_000];
        Console.WriteLine($"  byte[84000]  (< 85KB) : Gen{GC.GetGeneration(small)}  (通常ヒープ / SOH)");
        Console.WriteLine($"  byte[85000]  (>=85KB) : Gen{GC.GetGeneration(large)}  (LOH = Gen2 扱い)");
        GC.KeepAlive(small);
        GC.KeepAlive(large);
        Console.WriteLine();
    }

    // ------------------------------------------------------------------------
    // Demo4: アロケーション量と GC 停止時間(stop-the-world)の関係
    // ------------------------------------------------------------------------
    private static void Demo4_PauseTime()
    {
        Console.WriteLine("[Demo4] アロケーション量と GC 停止時間");
        const int N = 20_000_000;
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        int g0 = GC.CollectionCount(0);
        TimeSpan before = GC.GetTotalPauseDuration();
        for (int i = 0; i < N; i++)
        {
            Blackhole = new byte[64];
            Blackhole[0] = (byte)i;
        }
        TimeSpan pause = GC.GetTotalPauseDuration() - before;
        int gens = GC.CollectionCount(0) - g0;
        Console.WriteLine($"  new byte[64] を {N:N0}回: Gen0 GC {gens}回, GC 停止合計 {pause.TotalMilliseconds:F1} ms");
        Console.WriteLine();
    }
}
