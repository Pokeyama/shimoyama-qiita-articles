---
title: DFrameで負荷試験を始めたら「Ramp-Up」が無かった話
tags:
  - C#
  - .NET
  - gRPC
  - 負荷試験
  - DFrame
private: false
updated_at: ''
id: null
organization_url_name: advancednet-inc
slide: false
ignorePublish: false
---
# はじめに

業務でMagicOnion(gRPC+MessagePack)なAPIサーバーの負荷試験をすることになりました。

負荷試験ツールといえばk6やJMeterが定番ですが、あれらはHTTP/JSONを前提にしたツールです。MessagePackでシリアライズされたgRPCを叩く手段がないので、MagicOnionのAPIには使えません（JSON変換用のエンドポイントを別に生やして叩く手はありますが、本番と別経路を計測しても数字の意味がないので却下）。

そこで[DFrame](https://github.com/Cysharp/DFrame)です。Cysharp製の分散負荷試験フレームワークで、**シナリオをC#で書ける**ため、gRPCだろうがMagicOnionだろうがクライアントコードがそのまま負荷シナリオになります。

使ってみたら概ね快適だったのですが、1つだけ「あれ？」となったことがあります。**負荷を徐々に上げていくRamp-Up機能が無い**。この記事は、DFrameの基本的な使い方と、「Ramp-Upが無いのは仕様なのか？」を調べた記録です。

# 対象読者

- gRPC/MagicOnionなAPIに負荷試験をしたいがk6が使えなくて困っている人
- DFrameを触り始めたが、Concurrency/Worker Limit/Repeatあたりのパラメータがピンときていない人

# 環境

- Mac M3
- .NET 10
- DFrame 2.0.0

# DFrameの構成

DFrameはControllerとWorkerの2つで構成されます。

```mermaid
graph LR
    B[ブラウザ/REST] -->|実行指示| C[Controller<br>Web UI + 結果集計]
    C -->|gRPCで配信| W1[Worker 1<br>シナリオ実行]
    C -->|gRPCで配信| W2[Worker 2]
    C -->|gRPCで配信| W3[Worker N]
    W1 --> API[(試験対象API)]
    W2 --> API
    W3 --> API
```

- **Controller**: Web UIと結果集計を担当。ここのブラウザ画面から実行を指示する
- **Worker**: シナリオを実行する側。Controllerに常時gRPC接続していて、指示を受けたら一斉に負荷をかける

「分散」フレームワークなのでWorkerを何台も並べられますが、1プロセスにControllerとWorkerを同居させることもできて、ローカルで試すだけならバイナリ1個で完結します。

# 最小構成で動かす

NuGetで`DFrame`を入れて、Program.csにこれだけ書けば動きます。

```csharp:Program.cs
using DFrame;

DFrameApp.Run(7312, 7313); // WebUI:7312, Worker接続:7313

public class SampleWorkload : Workload
{
    public override async Task ExecuteAsync(WorkloadContext context)
    {
        Console.WriteLine($"Hello {context.WorkloadId}");
    }
}
```

DFrameではテストシナリオを`Workload`と呼びます。`Workload`を継承して`ExecuteAsync`を書くと、Web UIのセレクトボックスに自動で並びます。

1点だけ注意で、ControllerはASP.NET Coreで動くため、csprojのSdkを`Microsoft.NET.Sdk.Web`にして`RequiresAspNetWebAssets`を足す必要があります（.NET 10の場合）。

```xml:csproj
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <RequiresAspNetWebAssets>true</RequiresAspNetWebAssets>
  </PropertyGroup>
</Project>
```

起動して http://localhost:7312 を開くとWeb UIが出ます。

<!-- TODO(スクショ1): Web UIのトップ画面。Workloadセレクトボックスに自作Workloadが並んでいて、Concurrency/Total Request/Worker Limitの入力欄が見える状態。EXECUTEボタン押下前でOK -->
![Web UIトップ画面](ここにスクショ1)

# シナリオ(Workload)の書き方

`ExecuteAsync`のほかに`SetupAsync`/`TeardownAsync`があるので、接続の確立や後始末はそちらに書きます。gRPCの例がこうです。

```csharp:gRPCを叩くWorkload
public class GrpcTest : Workload
{
    GrpcChannel? channel;
    Greeter.GreeterClient? client;

    public override async Task SetupAsync(WorkloadContext context)
    {
        channel = GrpcChannel.ForAddress("http://localhost:5027");
        client = new Greeter.GreeterClient(channel);
    }

    public override async Task ExecuteAsync(WorkloadContext context)
    {
        await client!.SayHelloAsync(new HelloRequest(), cancellationToken: context.CancellationToken);
    }

    public override async Task TeardownAsync(WorkloadContext context)
    {
        if (channel != null)
        {
            await channel.ShutdownAsync();
            channel.Dispose();
        }
    }
}
```

MagicOnionなら`SetupAsync`で`MagicOnionClient.Create<IMyService>(channel)`する形になるだけで、構造は同じです。

地味に重要なのが、**Setupは計測に含まれない**ことです。チャンネル確立や認証・ユーザー登録などの準備をSetupに寄せておけば、`ExecuteAsync`の計測値が純粋な本番リクエストの数字になります。しかも全Workloadの準備完了を待ってから一斉に計測が始まるので、準備の遅速で開始タイミングがバラつくこともありません。

コンストラクタで引数も受け取れます。プリミティブな引数はWeb UIの入力欄になり、DIコンテナに登録した型はそのまま注入されます。

```csharp:引数とDI
var builder = DFrameApp.CreateBuilder(7312, 7313);
builder.ConfigureServices(services =>
{
    services.AddSingleton<HttpClient>();
});
await builder.RunAsync();

public class HttpGetString : Workload
{
    readonly HttpClient httpClient; // DIから注入
    readonly string url;            // Web UIの入力欄になる

    public HttpGetString(HttpClient httpClient, string url)
    {
        this.httpClient = httpClient;
        this.url = url;
    }

    public override async Task ExecuteAsync(WorkloadContext context)
    {
        await httpClient.GetStringAsync(url, context.CancellationToken);
    }
}
```

# パラメータの読み方

最初に混乱するのがここだと思うので整理します。共通パラメータは3つです。

| パラメータ | 意味 |
|---|---|
| Concurrency | **Worker 1台の中に**作るWorkloadインスタンス数。この数だけ`ExecuteAsync`が並列実行される |
| Worker Limit | 使うWorkerの台数 |
| Total Request | `ExecuteAsync`の総実行回数。全Worker合計 |

つまり**同時並列数=Worker台数×Concurrency**です。公式READMEの例だと、Worker4台×Concurrency10でWorkloadインスタンスは40個、並列実行数も40になります。1台あたりの実行回数はTotal Request÷Worker台数÷Concurrencyで割り付けられます。

もう1つ大事なのが計測単位です。DFrameの1リクエスト=`ExecuteAsync`1回なので、シナリオ内で複数APIを呼ぶと「RPS」はAPI呼び出し数/秒ではなくシナリオ完了数/秒になります。API単位のRPSが欲しい場合は換算が要る点に注意です。

また、DFrameはクローズドループ型（前のリクエストが返ってきたら次を投げる）なので、**サーバーが遅くなると投げる側も自動で遅くなります**。「毎秒◯リクエストを投げ続けて限界を見る」オープンループ型とは性質が違う、というのは頭の片隅に置いておくといいです。

# 実行モードは4つ

| モード | 動き |
|---|---|
| Request | Total Request回実行して終了。基本のモード |
| Repeat | Requestを完了するたびに、Total RequestとWorker Limitを増分して繰り返す |
| Duration | 指定秒数だけ実行し続ける |
| Infinite | STOPを押すまで無限に実行 |

Durationの注意点として、時間切れの瞬間に飛行中だったリクエストはキャンセル扱いでエラーに計上されます。試験終盤にエラーが数件出ていても、それは時間切れの打ち切り分かもしれないので、errorMessageを見て切り分けるのがおすすめです。

# で、Ramp-Upはどこ？

負荷試験ツールにはたいてい、目標の負荷まで仮想ユーザーを徐々に投入していくRamp-Up機能があります。k6なら`stages`、JMeterならThread GroupのRamp-Up Periodです。DFrameで同じものを探しました。

**無い。**

READMEを読み直すと、Repeatモードの説明にこう書いてあります。

> Repeat is similar as Ramp-Up. After request completed, increase TotalRequest and WorkerLimit.

「RepeatはRamp-Upに似たもの」。つまり滑らかに増やす機能は無くて、**段階的に増やすRepeatがその代替**という位置付けです。

裏も取れました。[issue #40](https://github.com/Cysharp/DFrame/issues/40)でまさに「Locustのspawn rate相当はどう設定するのか」という質問があり、Cysharpのメンバーがこう回答しています。

> Unfortunately, there seems to be no default setting to gradually increase concurrency.
>
> It may be easier to run multiple executions in duration mode, changing the settings as you go.

「Concurrencyを徐々に増やすデフォルト設定は無い。設定を変えながらDurationモードで複数回実行するのが楽だろう」とのこと。**Ramp-Upが無いのは実装漏れではなく割り切り**と見てよさそうです。

# そもそもRamp-Upは何のためにあるのか

「Ramp-Upが無い」と聞くと不安になりますが、Ramp-Upが担っている役割を分解すると、DFrameに無くて困るものはほとんど残りません。役割は大きく2つあります。

**1つ目は、立ち上がりをばらけさせる助走です**。こちらが本来の主目的で、試験開始の瞬間に全仮想ユーザーが一斉に接続・認証・初回リクエストを始めると、計測したい定常負荷とは別物の開始スパイクがサーバーにかかります。これを避けるために投入を時間方向にばらすのがRamp-Upです。この使い方では**ランプ区間は計測の本体ではありません**。目標負荷に達したあとの定常区間が本体で、ランプ中のサンプルは定常状態の値ではないため、分析から外して評価するのが定石です（JMeterのRamp-Up Periodもレポートにはランプ中のサンプルが混ざるので、外して読む側の運用になります）。

**2つ目は、壊れる位置を探す傾斜としての使い方です**。ストレス試験として負荷を上げ続け、どこからエラーや性能劣化が始まるかを見る。こちらはランプそのものが試験対象です。

この2つ、DFrameではこうなります。

## 助走はSetupの分離が代わりに担っている

DFrameには助走がそもそも要りません。接続確立・認証・テストユーザー登録といった準備は`SetupAsync`に書けば計測外で実行され、**全Workloadの準備完了を待ってから一斉に計測が始まる**からです。開始時に一斉なのは「意図した同時並列数ちょうど」のリクエストであって、接続や認証のストームにはなりません。ランプ中の値が計測に混ざる問題も、外して読む手間も、構造的に発生しないわけですね。

## 壊れる位置探しはRepeatの段階負荷で代替する

<!-- TODO(画像): リポジトリ直下のdframe-load-shape.pngをQiitaへアップロードしてURLを差し替える -->
![負荷のかかり方のイメージ](ここにdframe-load-shape.png)

線形の傾斜は「エラーが出始めた瞬間の負荷」をピンポイントで特定できますが、計測値が全部「変化し続ける負荷の下での値」になるため、「同時100のときのp99は？」という問いに答えるには結局その負荷で維持した区間が必要になります。

段階（Repeat）は各段が独立した試験として完結します。DFrameのRepeatは段ごとに結果が別レコードで残るので、

- 「同時50までp99は安定、同時60からエラー率が跳ねた」と段単位で比較できる
- 負荷が変化している途中の過渡状態が計測に混ざらない

という利点があります。壊れる位置の分解能は段の粗さに依存しますが、そこは段を細かく刻めばいいだけです。容量計画（このスペックで同時何人まで捌けるか）が目的なら、段階負荷で困ることはほぼ無い、というのが使ってみた感想です。

残るのは、瞬間的なスパイク耐性やオートスケールの追従速度など**負荷の傾斜そのものが試験対象**のケースだけで、そこはDFrameの守備範囲外です。

# Repeatで階段負荷を組む

ここからが実践編です。Repeatの増分パラメータは`Increase Total Request`と`Increase Worker`の2つで、**Concurrencyは増やせません**（全段で固定）。つまり負荷を上げる軸は**Worker台数**です。

「Workerそんなに並べられないんだけど」と思いますよね。私も思いました。ここで効くのがWorkerの`VirtualProcess`オプションです。

```csharp:Worker側の設定
var builder = DFrameApp.CreateBuilder(7312, 7313);
builder.ConfigureWorker(options =>
{
    options.VirtualProcess = 10; // 1プロセスを10台のWorkerに見せる
});
await builder.RunAsync();
```

これで1プロセスがControllerからは10台に見えます（Controllerへの接続ソケットが10本になるだけで、負荷をかける物理マシンは1台のままです）。READMEにも「実マシンを複数並べる場合は紛らわしいので、Workerが単一プロセスのときだけ使うのを推奨」とあります。

この状態で、たとえばこう設定します。

| 設定 | 値 |
|---|---|
| Concurrency | 10 |
| Total Request | 100 |
| Worker Limit | 1 |
| Increase Total Request | 100 |
| Increase Worker | 1 |
| Repeat | 10 |

すると各段はこうなります。

| 段 | Worker台数 | 同時並列数(台数×Concurrency) | Total Request |
|---|---|---|---|
| 1 | 1 | 10 | 100 |
| 2 | 2 | 20 | 200 |
| 3 | 3 | 30 | 300 |
| … | … | … | … |
| 10 | 10 | 100 | 1000 |

同時10から100まで、10刻みの階段負荷です。各段で1インスタンスあたりの実行回数（Total Request÷台数÷Concurrency=10回）が一定になるようIncrease Total Requestを合わせておくと、段ごとの試験時間も揃って比較しやすくなります。

<!-- TODO(スクショ2): Web UIでREPEATモードを選択し、上の表の値(Concurrency=10, Total Request=100, Worker Limit=1, Increase Total Request=100, Increase Worker=1, Repeat=10)を入力した設定画面 -->
![Repeatモードの設定画面](ここにスクショ2)

実行すると、段ごとの結果が履歴に積まれていきます。

<!-- TODO(スクショ3): Repeat実行後の結果画面。段ごとにRPS/レイテンシが別レコードで並んでいるのが分かる部分。可能なら負荷が上がるにつれてlatencyが伸びていく様子が見える結果だと説得力が出ます -->
![Repeat実行結果](ここにスクショ3)

# ハマった：REST APIのRepeatは増分が効かない

DFrameにはCIから叩けるREST APIがあって、`POST /api/repeat`でRepeatモードを実行できます。Web UIで組んだ階段をそのままRESTに移植したところ、**何段実行しても負荷が上がらない**。というか実際に「階段のつもりが平地だった」試験結果を量産しました。

原因はDFrame本体（2.0.0時点）のRestApi.csにあります。

```csharp:DFrame.Controller/RestApi.cs(抜粋)
repeatModeState = new Pages.RepeatModeState(
    request.Workload, request.Concurrency, request.TotalRequest,
    request.IncreaseTotalWorker,  // ← 第4引数はincreaseTotalRequestなのに…
    workerLimit,
    request.IncreaseTotalWorker,
    request.RepeatCount, ...);
```

`RepeatModeState`のコンストラクタは第4引数が`increaseTotalRequest`なのですが、そこに`request.IncreaseTotalWorker`が渡っています。つまり**RESTのリクエストボディに書いた`IncreaseTotalRequest`はどこにも使われません**。Web UI経由は正しいコードパスを通るので、この問題はREST限定です。

ワークアラウンドは「増やしたい値を`IncreaseTotalWorker`に入れる」です。上のコードの通り、`IncreaseTotalWorker`はTotal Requestの増分とWorker Limitの増分の**両方**に渡るので、Worker Limitも一緒に増えてしまいますが、実Worker数を超えたWorker Limitは接続数で頭打ちになるだけなので、Worker構成によっては実害なく回避できます。手元ではTotal Requestが10→20→30と意図通り増えることを確認しました。

1行直せば済む話なので、修正PRを本家に送る予定です。

# まとめ

- MagicOnion/gRPC(MessagePack)のAPIはk6等のHTTP系ツールで叩けないので、C#でシナリオを書けるDFrameがほぼ一択です
- DFrameにRamp-Upはありません。issue #40での回答を見る限り実装漏れではなく割り切りです
- Ramp-Upの主目的である「立ち上がりをばらけさせる助走」は、DFrameではSetupの分離（準備は計測外＋全員の準備完了を待って一斉スタート）が構造的に担っています
- もう1つの用途「壊れる位置探し」はRepeatモードの段階負荷で代替します。同時並列数=Worker台数×Concurrencyで、Concurrencyは固定なので、`VirtualProcess`で台数を稼いでWorker台数を軸に階段を組みます
- 段階負荷は各段が独立した計測結果として残るので、容量計画目的ならむしろ読みやすいです。負荷の傾斜そのもの（スパイク・オートスケール追従）を試験したい場合だけ別のツールを検討してください
- `POST /api/repeat`の`IncreaseTotalRequest`は2.0.0時点で効きません。増分は`IncreaseTotalWorker`に入れて回避します
