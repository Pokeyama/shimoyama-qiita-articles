---
title: 【ID採番】いい加減説明できるようにしたいSnowflakeとSonyflake
tags:
  - C#
  - Snowflake
  - Sonyflake
private: false
updated_at: ''
id: null
organization_url_name: advancednet-inc
slide: false
ignorePublish: false
posting_campaign_uuid: null
agreed_posting_campaign_term: false
---
# はじめに
ID採番の話でSnowflakeが出てきたのですが、「時刻が入った64bitのID」くらいの理解で、中身は説明できませんでした。
Sony製のSonyflakeというのもあるらしいので、両方まとめて違いとか使いどころを記事にします。

# 環境
macOS
.NET 10.0.8

# Snowflakeとは
2010年6月にTwitterのエンジニアリングブログで「Announcing Snowflake」として発表された由緒正しいパッケージです。
開発目的はツイートのIDを作るための仕組みとのことです。

https://blog.x.com/engineering/en_us/a/2010/announcing-snowflake

GitHubのtwitter-archive/snowflakeも同じ2010年6月の作成で、今はアーカイブされています。
READMEには「Snowflake is a network service for generating unique ID numbers at high scale with some simple guarantees.」とあります。

https://github.com/twitter-archive/snowflake/tree/snowflake-2010

ライブラリではなくID専用のネットワークサービスとして作られていた、というのが元祖の形みたいです。
ワーカーIDは起動時の設定で渡して、ZooKeeperへ登録します。
ID採番用に別にサーバーを持っておくことを想定して作られたものというのがミソだと思います。

# 64bitの中身
構成は以下のようになっています。
上位から順に並んでいます。

```text
符号1bit（使わない） | 時刻41bit | データセンターID 5bit | ワーカーID 5bit | シーケンス12bit
```

時刻は「起点から何ミリ秒経ったか」の数です。
元のコードの起点（twepoch）は`1288834974657`で、2010年11月4日ごろです。

## 組み立て
例として、以下の値からIDを作ります。

```text
時刻             : 2026-10-31 12:00:00 UTC（起点から504,613,025,343ミリ秒）
データセンターID : 3
ワーカーID       : 17
シーケンス       : 42
```

できあがるIDは`2116500430648709162`です。

ポイントは2進数で1bit左にずらすと2倍になることです。
基本情報で勉強したことを思い出しましょう。
以下のように確認できます。

```c#
Console.WriteLine(5);
Console.WriteLine(5 << 1);
Console.WriteLine(5 << 2);
Console.WriteLine(5 << 3);
```

```text
5
10
20
40
```

2進数で見ると、右に0が増えているだけです。
`Convert.ToString(値, 2)`で2進数の文字列にできます。

```c#
Console.WriteLine(Convert.ToString(5, 2));
Console.WriteLine(Convert.ToString(5 << 1, 2));
Console.WriteLine(Convert.ToString(5 << 2, 2));
Console.WriteLine(Convert.ToString(5 << 3, 2));
```

```text
101
1010
10100
101000
```

なので12bit分あけて、その上にワーカーID、その上にデータセンターID、と積んでいけば、IDは以下の式で組み立てられます。

```text
ID = 時刻 << 22 | データセンターID << 17 | ワーカーID << 12 | シーケンス
```

10進数で書くなら`時刻 × 2^22 + データセンターID × 2^17 + ワーカーID × 2^12 + シーケンス`です。
最初の値をコードで組み立てると、以下のようになります。

```c#
const long TwEpoch = 1288834974657L;
var now = DateTimeOffset.Parse("2026-10-31T12:00:00Z");
long elapsed = now.ToUnixTimeMilliseconds() - TwEpoch;
long dc = 3, worker = 17, seq = 42;
long id = (elapsed << 22) | (dc << 17) | (worker << 12) | seq;
```

```text
elapsed(ms) = 504,613,025,343
id          = 2116500430648709162
binary      = 1110101011111010100011110110110001111110001110001000000101010
id          = 504,613,025,343 * 2^22 + 3 * 2^17 + 17 * 2^12 + 42 = 2116500430648709162
```

シフトで作ったIDと、掛け算と足し算で作ったIDが一致していることがわかります。

## 取り出し
元に戻すときは右にずらして、必要なbit数だけマスクします。

```c#
long dElapsed = id >> 22;
long dDc = (id >> 17) & 0x1F;
long dWorker = (id >> 12) & 0x1F;
long dSeq = id & 0xFFF;
```

```text
elapsed=504,613,025,343 dc=3 worker=17 seq=42
time=2026-10-31T12:00:00.0000000+00:00
```

なので、IDから作った時刻と、どのデータセンターのどのワーカーかまで読めます。
`0x1F`は5bit、`0xFFF`は12bitが全部1の値です。

## 何年持つか
時刻は41bitなので、表せるミリ秒は`2^41`です。
これを1年のミリ秒で割ります。

```text
2^41 ms = 2199023255552 ms = 69.68 years
twepoch起点の期限: 2080-07-10 17:30:30.209 UTC
1970起点の期限   : 2039-09-07 15:47:35.552 UTC
```

起点を2010年にずらしているのは、この寿命を後ろに伸ばすためなんだと思います。
Unix時刻そのままの1970年起点だと2039年であふれます。
起点を1970年にしてるサービス結構ありそうですが、もしかしたら2039年が近くなったらいろいろ混乱が起きてそうですね。

## 1ミリ秒に作れる数
シーケンスは、同じミリ秒の中で何個目のIDかを表す番号です。
ミリ秒ごとに配り直す整理券みたいなもので、0から始まって、ミリ秒が変わると0に戻ります。
最初の例のシーケンス42は、そのミリ秒で43個目のIDという意味です。

12bitで表せるのは0〜4095なので、1台のワーカーが1ミリ秒に作れるIDは4096個です。
1秒にすると約409万個です。
4097個以上のIDが欲しくなったら、次のミリ秒になるまで待つ必要があります。

# Sonyflakeとは
Sonyが公開しているGo製のライブラリです。
GitHubのsony/sonyflakeは2015年の作成でMITライセンスです。v1.0.0が2019年、v2.0.0が2025年に出ています。

https://github.com/sony/sonyflake

READMEには「Sonyflake is a distributed unique ID generator inspired by Twitter's Snowflake.」とあります。
寿命と、ホストやコアが多い環境での性能を重視しているとのこと。
Snowflakeがネットワークサービスだったのに対して、こちらはライブラリです。

## 64bitの中身
既定の構成は以下です。符号を除いた63bitです。

```text
時刻39bit（10ミリ秒単位） | シーケンス8bit | マシンID 16bit
```

並びは時刻→シーケンス→マシンIDで、Snowflakeとは順番が違います。
マシンIDが一番下に来ています。
組み立ては同じくシフトです。

```c#
var sfEpoch = DateTimeOffset.Parse("2025-01-01T00:00:00Z");
long unit = (now - sfEpoch).Ticks / TimeSpan.TicksPerMillisecond / 10;
long machine = (10L << 8) | 5;
long sSeq = 7;
long sid = (unit << (8 + 16)) | (sSeq << 16) | machine;
```

```text
elapsed(10ms) = 5775840000
id            = 96902515261901317
取り出し: elapsed=5775840000 seq=7 machine=2565
```

マシンIDの2565は、IPの下位16bitが`10.5`のとき（10×256+5）の値です。

起点はv1が2014年、v2が2025年なので、v2の値を使っています。
時刻の単位が10msなので、経過した数も10ms単位になります。

## 何年持つか
時刻39bit×10msで、`2^39 × 10ms`を1年で割ります。

```text
2^39 * 10ms = 5497558138880 ms = 174.21 years
v2起点の期限: 2199-03-19 UTC
```

Snowflakeの約69年に対して約174年です。
時刻のbitは41から39に減っていますが、単位を10msにしたので少ないbitでも長く持つようにしているようです。

## 1台あたりの数と台数
シーケンスは8bitなので10msに256個、1秒に25,600個です。
Snowflakeは1ミリ秒に4096個なので、1台あたりの数はかなり少ないです。
READMEでも欠点として挙げられています。

> It can generate 2^8 IDs per 10 msec at most in a single instance (fewer than Snowflake)

その代わりマシンIDが16bitあるので、2^16台で動きます。Snowflakeは5bit+5bitで2^10です。

# SnowflakeとSonyflakeの比較
それぞれを比較するとこんな感じになりそうです。

| | Snowflake | Sonyflake |
|---|---|---|
| 時刻 | 41bit・1ms単位 | 39bit・10ms単位 |
| シーケンス | 12bit（1msに4096個） | 8bit（10msに256個） |
| マシンID | データセンターID 5bit+ワーカーID 5bit（2^10） | マシンID 16bit（2^16） |
| 並び | 時刻→データセンター→ワーカー→シーケンス | 時刻→シーケンス→マシンID |
| 寿命 | 約69.68年 | 約174.21年 |

# マシンIDの決め方
ちゃんと考えようとするとこのマシンIDの採番に難儀させられます。
SnowflakeのワーカーIDは使う側で配る必要があって、元祖はZooKeeperで重複を防いでいました。
Sonyflakeの既定は、プライベートIPv4アドレスの下位16bitです。
例えばサーバーのIPが`10.0.10.5`なら、以下のようになります。

```text
IP       : 10.0.10.5
後ろ2つ  : 10.5
2進数    : 00001010 00000101
マシンID : 10 × 256 + 5 = 2565
```

IPの1区切りは8bitなので、`10`と`5`を2進数でくっつけただけです。
組み立てで使った2565はこの値です。

どのIPを使うかは、ネットワークインターフェースのアドレスを順に見て、10/8、172.16/12、192.168/16、169.254/16のどれかに当てはまった最初のものです。

READMEには、AWSのVPCは普通/16〜/28のCIDRなので、各EC2のIPがかぶらなければ下位16bitもかぶらない、という前提が書いてあります。
例えばVPCが`10.0.0.0/16`なら、どのサーバーも`10.0.x.x`で前半が同じなので、後ろ2つが違えばマシンIDも違います。

逆に、`10.0.0.0/16`と`10.1.0.0/16`のVPCに`10.0.10.5`と`10.1.10.5`のサーバーがあると、どちらもマシンIDは2565になってかぶります。
IPから自動で決めるなら、自分のネットワークがこの前提に収まっているか確認しておきましょう。

# IDがかぶるとき
同じ時刻単位の中でシーケンスを使い切ると、放っておけばかぶります。
なので両方とも次の時刻単位まで待つ作りで、Snowflakeは`tilNextMillis`のループ、Sonyflakeは`time.Sleep`で待つっぽいです。

時計が戻ったときは、Snowflakeは例外です。
Sonyflakeは例外にしないで、前回の時刻のままシーケンスを進めるみたいです。コードを見る限りなので、動かして確かめてはいません。

マシンIDが重複するのは配る側の問題です。
SnowflakeはワーカーIDを設定で渡して、かぶっていないかは起動時にZooKeeperで確かめていました。
Sonyflakeは、IPの前提に頼るか、`CheckMachineID`という用意されているフィールドの自前のチェックを書くことになります。

```go
type Settings struct {
        ...
        MachineID      func() (int, error)
        CheckMachineID func(int) bool
}
```
# 使い所
共通しているのは、DBの連番に聞きに行かずに各サーバーでIDを作りたい、時刻順に並ぶ64bit整数が欲しいときです。

Snowflakeは、台数が少なくて番号を固定で配れるなら素直に使えそうです。1台あたりの生成数も多いので。

Sonyflakeは台数が多くてIPからマシンIDを自動で決めたいとき、あとは寿命を長くしたいときです。
自分なら1秒に25,600個で足りるかと、IPがREADMEの前提に収まっているかを先に見るかなと思います。

# まとめ
ビット演算ってたまにやると頭が混乱する。
ID採番の銀の弾丸っぽく使われてますが、マシンIDの決め方が難しいので万能というわけではなさそうです。。

# 参考
https://blog.x.com/engineering/en_us/a/2010/announcing-snowflake

https://github.com/twitter-archive/snowflake/tree/snowflake-2010

https://github.com/sony/sonyflake

https://pkg.go.dev/github.com/sony/sonyflake/v2
