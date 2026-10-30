// Snowflake: 時刻41 | DC5 | Worker5 | Seq12
const long TwEpoch = 1288834974657L; // 2010-11-04 01:42:54.657 UTC
var tw = DateTimeOffset.FromUnixTimeMilliseconds(TwEpoch);

Console.WriteLine("== 1bit左にずらすと2倍");
Console.WriteLine($"5 = {Convert.ToString(5, 2)}, 5 << 1 = {Convert.ToString(5 << 1, 2)} ({5 << 1}), 5 << 3 = {5 << 3}");

Console.WriteLine("== Snowflake 組み立て");
var now = DateTimeOffset.Parse("2026-10-31T12:00:00Z");
long elapsed = now.ToUnixTimeMilliseconds() - TwEpoch;
long dc = 3, worker = 17, seq = 42;
long id = (elapsed << 22) | (dc << 17) | (worker << 12) | seq;
Console.WriteLine($"elapsed(ms) = {elapsed:N0}");
Console.WriteLine($"id          = {id}");
Console.WriteLine($"binary      = {Convert.ToString(id, 2)}");
Console.WriteLine($"id          = {elapsed:N0} * 2^22 + {dc} * 2^17 + {worker} * 2^12 + {seq} = {elapsed * (1L << 22) + dc * (1L << 17) + worker * (1L << 12) + seq}");

Console.WriteLine("== Snowflake 取り出し");
long dElapsed = id >> 22;
long dDc = (id >> 17) & 0x1F;
long dWorker = (id >> 12) & 0x1F;
long dSeq = id & 0xFFF;
Console.WriteLine($"elapsed={dElapsed:N0} dc={dDc} worker={dWorker} seq={dSeq}");
Console.WriteLine($"time={DateTimeOffset.FromUnixTimeMilliseconds(dElapsed + TwEpoch):O}");

Console.WriteLine("== Snowflake 寿命");
const double YearMs = 365.2425 * 24 * 3600 * 1000;
Console.WriteLine($"2^41 ms = {1L << 41} ms = {(1L << 41) / YearMs:F2} years");
Console.WriteLine($"twepoch起点の期限: {tw.AddMilliseconds(1L << 41):yyyy-MM-dd HH:mm:ss.fff} UTC");
Console.WriteLine($"1970起点の期限   : {DateTimeOffset.UnixEpoch.AddMilliseconds(1L << 41):yyyy-MM-dd HH:mm:ss.fff} UTC");
Console.WriteLine($"1ミリ秒あたりの個数: {1 << 12}");

Console.WriteLine("== Sonyflake 組み立て(10ms単位 / 時刻39 | Seq8 | Machine16)");
var sfEpoch = DateTimeOffset.Parse("2025-01-01T00:00:00Z"); // v2の既定
long unit = (now - sfEpoch).Ticks / TimeSpan.TicksPerMillisecond / 10;
long machine = (10L << 8) | 5; // 10.0.x.y の下位16bitの例: x=10, y=5 -> 10.0.10.5 だとこの値
long sSeq = 7;
long sid = (unit << (8 + 16)) | (sSeq << 16) | machine;
Console.WriteLine($"elapsed(10ms) = {unit}");
Console.WriteLine($"id            = {sid}");
Console.WriteLine($"binary        = {Convert.ToString(sid, 2)}");
Console.WriteLine($"取り出し: elapsed={sid >> 24} seq={(sid >> 16) & 0xFF} machine={sid & 0xFFFF}");
Console.WriteLine($"2^39 * 10ms = {(1L << 39) * 10} ms = {(1L << 39) * 10 / YearMs:F2} years");
Console.WriteLine($"v2起点の期限: {sfEpoch.AddMilliseconds((1L << 39) * 10):yyyy-MM-dd} UTC");
Console.WriteLine($"10msあたりの個数: {1 << 8} = 1秒あたり {(1 << 8) * 100}");
