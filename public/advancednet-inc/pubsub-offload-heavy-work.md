---
title: Pub/Subで重い処理を非同期に逃がそう
tags:
  - PubSub
  - GoogleCloud
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
業務でGCPによるPub/Subでのキューイング実装をすることになりそうなのでもろもろ検証して勉強してみました。
AWSのSQS/SNSは使用したことあったのですが、こちらは使用したことなかったので。

# 環境
macOS 26.5.2
.NET SDK 10.0.300
Google.Cloud.PubSub.V1 3.39.0
MySqlConnector 3.0.0
MySQL 8.4 (Docker)

# Pub/Subとは
GCPのメッセージングサービスです。
送信側と受信側の間にPub/Subを挟んで、データを受け渡します。
Pub/Subという名前は、送信するpublishと、購読するsubscribeから来ています。

![pubsub-basics.png](https://qiita-image-store.s3.ap-northeast-1.amazonaws.com/0/855584/25c614b1-1231-4c6c-84e5-dd49c529f776.png)
送るデータをメッセージ、送信するプログラムをパブリッシャーと呼びます。

## メッセージが届くまで

パブリッシャーは、トピック（送信先）にメッセージを送信（publish）します。
メッセージは、トピックにつないだ各サブスクリプション（受信口）へ配られます。
サブスクライバーが、そこからメッセージを受信して処理します。

この記事では、注文受付APIがパブリッシャー、注文処理のワーカーがサブスクライバーです。

サブスクリプションを注文処理用と通知用の2つ作れば、両方に同じ注文IDを届けることができます。

https://docs.cloud.google.com/pubsub/docs/pubsub-basics?hl=ja

## ackとnack
ackは、ワーカーがPub/Subに返す処理完了の通知です。
メッセージを受け取っただけでは処理済みにならず、処理が終わってからackを返します。

ack期限は、Pub/Subがメッセージを届けてからackを待つ時間です。
この期限までにackが返ってこないと、Pub/Subはメッセージを再送します。

nackは、処理できなかったことを伝える通知です。
nackを返すと、ack期限を待たずに再送の対象になります。
実際に再送されるまでの待ち時間は、後述する再試行ポリシーにも影響されます。

https://docs.cloud.google.com/pubsub/docs/subscription-overview?hl=ja

## pullとpush
自分のプログラムでメッセージを受け取る方法には、pullとpushがあります。
pullはワーカーからPub/Subにメッセージを取りに行く方式で、pushはPub/SubがワーカーのURLにHTTPリクエストを送る方式です。
pushでは`200`などのHTTP応答がackに相当し、エラー応答やタイムアウトで再送の対象になります。

https://docs.cloud.google.com/pubsub/docs/push?hl=ja

この記事はpullを使います。

## AWSとの対応
AWSにもSQS/SNSという似たようなサービスがあります。
そちらを使ったことがある方はこの対応表でなんとなくわかるかなと思います。
SNSからSQSに配信する構成で考えると、以下のような対応です。

| Pub/Sub | AWS |
| --- | --- |
| トピック | SNSのトピック |
| サブスクリプション | SQSのキュー |
| ack期限 デフォルト10秒 | SQSの可視性タイムアウト デフォルト30秒 |
| nack | 可視性タイムアウトを0にする |
| dead letterトピック | SQSのデッドレターキュー |
| 未ackのメッセージの保持期間 デフォルト7日・最長31日 | SQSのメッセージ保持期間 デフォルト4日・最長14日 |

https://docs.cloud.google.com/pubsub/docs/dead-letter-topics?hl=ja

https://docs.cloud.google.com/pubsub/docs/subscription-properties?hl=ja

https://docs.aws.amazon.com/ja_jp/sns/latest/dg/sns-sqs-as-subscriber.html

https://docs.aws.amazon.com/ja_jp/AWSSimpleQueueService/latest/SQSDeveloperGuide/sqs-visibility-timeout.html

# 構成
今回は、注文を受け付ける簡単な注文システムを想定して実際に構築してみます。
注文をDBに保存して、その注文に対する重い処理をする、という流れです。

これをAPIの中で全部やると、呼び出した側は重い処理が終わるまで応答を待つことになります。
APIでは注文の受付まで行って、重い処理はワーカーに任せます。
APIとワーカーを別サーバーに分ければ、重い処理に使うCPUやメモリをAPI側から切り離せます。

![order-flow.png](https://qiita-image-store.s3.ap-northeast-1.amazonaws.com/0/855584/7af59719-d77f-4401-9ee7-de4dbbed6c7d.png)

図はAPIとワーカーを別サーバーに分ける構成です。
今回の動作確認では、同じMac上で別プロセスとして動かしています。

APIは注文を受けたら、注文を`pending`（処理待ち）で保存して、Pub/Subに注文IDをpublishして受付結果を返します。
この時点では、注文の処理はまだ終わっていません。

ワーカーは、メッセージの注文IDでDBを見て、重い処理をして`done`（だん・処理完了）にします。

テーブルは以下のような感じです。

```sql:init.sql
CREATE TABLE orders (
  order_id BIGINT AUTO_INCREMENT PRIMARY KEY,
  status VARCHAR(16) NOT NULL,          -- 'pending' / 'done'
  process_count INT NOT NULL DEFAULT 0, -- doneへの更新回数を確認するため
  created_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  done_at DATETIME(3) NULL
);
```

重い処理は3秒待つようにして再現しています。


# トピックとサブスクリプションを作る
gcloudで作ります。

```console
$ gcloud pubsub topics create pubsub-lab-orders
Created topic [projects/<PROJECT>/topics/pubsub-lab-orders].
$ gcloud pubsub subscriptions create pubsub-lab-orders-worker --topic=pubsub-lab-orders \
    --ack-deadline=10 --min-retry-delay=10s --max-retry-delay=600s
Created subscription [projects/<PROJECT>/subscriptions/pubsub-lab-orders-worker].
```

`--ack-deadline`はデフォルトの10秒のまま。

`--min-retry-delay`と`--max-retry-delay`は再試行ポリシーの設定で、nackやack期限切れのあとに次を送るまでの待ち時間を指数バックオフで伸ばしていきます。

デフォルトの再試行ポリシーは「すぐに再試行」です。

> デフォルトでは、サブスクリプションの再試行ポリシーは「すぐに再試行」を使用するように設定されています。このオプションを使用すると、確認応答の期限が切れるか、サブスクライバーが否定確認応答で応答したときに、Pub/Sub がメッセージを再送信します。

https://docs.cloud.google.com/pubsub/docs/subscription-properties?hl=ja

処理できない状態ですぐに再送されると、同じ失敗を短い間隔で繰り返すことになります。
再試行の間隔を徐々に空けるため、今回は最小遅延10秒・最大遅延600秒に設定しました。

実際にnackを返し続けるワーカーを40分動かして、再送の間隔を見てみました。

```text
14.6, 16.9, 20.0, 22.0, 23.0, 30.0, 37.1, 43.1, 79.0, 88.1, 126.1, 150.1, 159.2, 236.1, 263.3, 512.4, 567.6（秒）
```

倍々ではなく、ばらつきながら伸びていきます。
最小遅延10秒の設定でも、2秒かからずに再送された回がありました。

公式にも、nackが多い場合は待ち時間が短くなったり、待たずに再送されたりすることがあると書かれています。
保証されないそうなので注意が必要ですね。

https://docs.cloud.google.com/pubsub/docs/subscription-retry-policy?hl=ja

# APIを書く
エンドポイントは以下のようにしてみました。
検証用のスイッチとログは省いています。

```c#:Program.cs（APIの抜粋）
var publisher = await new PublisherClientBuilder
{
    TopicName = TopicName.FromProjectTopic(project, "pubsub-lab-orders"),
}.BuildAsync();

app.MapPost("/orders/async", async () =>
{
    await using var conn = new MySqlConnection(db);
    await conn.OpenAsync();
    await using var tx = await conn.BeginTransactionAsync();
    var cmd = new MySqlCommand("INSERT INTO orders (status) VALUES ('pending')", conn, tx);
    await cmd.ExecuteNonQueryAsync();
    var id = cmd.LastInsertedId;

    // publishが返ってからコミット
    var messageId = await publisher.PublishAsync(id.ToString());
    await tx.CommitAsync();
    return Results.Json(new { orderId = id, messageId });
});
```

保存してからpublishして、最後にコミットする順番です。

Pub/Subへのpublishは、DBのトランザクションには入りません。
**ここで重要なのが順番で、「コミットしてからpublish」と「publishしてからコミット」のどちらかを選ぶことになります**。

## コミットしてからpublishすると取りこぼす可能性がある
![commit-then-publish.png](https://qiita-image-store.s3.ap-northeast-1.amazonaws.com/0/855584/7f77caaa-e543-4bb7-83a6-820878fd1fad.png)

コミットとpublishの間でプロセスが落ちると、DBには保存されているのにメッセージが送られません。
間でわざと落としてみると以下のようになりました。

```text
22:02:47.530 insert order=1
22:02:47.533 commit order=1
22:02:47.533 crash after commit
```

60秒待っても注文は`pending`のままで、ワーカーには1通も来ませんでした。

## publishしてからコミットする
![publish-then-commit.png](https://qiita-image-store.s3.ap-northeast-1.amazonaws.com/0/855584/b33a057a-56e9-4061-a5c6-b99e15984455.png)

このコードでは、`PublishAsync`が成功してからコミットします。
`PublishAsync`が例外になった場合はコミットせず、トランザクションの破棄時にロールバックされます。

代わりに、メッセージだけ届いてDBには注文IDが無い、という状態が起きます。
これはワーカーで吸収する必要があります。

送るメッセージをいったんテーブルに書いて、別のプロセスがpublishするOutboxパターンというやり方もありますが本題とズレるので省略。

# ワーカーを書く
ワーカーは以下のルールで書きます。

1. 注文が`pending` → 重い処理をして`done`にしてack
1. 注文が`done` → 何もしないでack
1. 注文が見つからない → 200ms待ってもう一度見る。それでも無ければnack

200ms待ってもう一度DBを見るのは、コミット待ちでnackになって再送を待つ回数を減らすためです。
見直しても注文が無ければ、あとでコミットされる可能性があるのでnackにします。

```c#:Program.cs（ワーカーの抜粋）
var subscriber = await new SubscriberClientBuilder
{
    SubscriptionName = SubscriptionName.FromProjectSubscription(project, "pubsub-lab-orders-worker"),
    ClientCount = 1,
    Settings = new SubscriberClient.Settings { FlowControlSettings = new FlowControlSettings(1, null) },
}.BuildAsync();

var run = subscriber.StartAsync(async (msg, ct) =>
{
    var id = long.Parse(msg.Data.ToStringUtf8());
    await using var conn = new MySqlConnection(db);
    await conn.OpenAsync(ct);
    async Task<string?> Select() => await new MySqlCommand("SELECT status FROM orders WHERE order_id=@id", conn)
        { Parameters = { new("@id", id) } }.ExecuteScalarAsync(ct) as string;

    var status = await Select();
    if (status is null)
    {
        // コミット待ち
        await Task.Delay(200, ct);
        status = await Select();
    }
    if (status is null) return SubscriberClient.Reply.Nack;
    if (status == "done") return SubscriberClient.Reply.Ack;

    // 重い処理の代わり
    await Task.Delay(3000, ct);
    await new MySqlCommand(
        "UPDATE orders SET status='done', process_count=process_count+1, done_at=NOW(3) "
        + "WHERE order_id=@id AND status='pending'",
        conn) { Parameters = { new("@id", id) } }.ExecuteNonQueryAsync();
    return SubscriberClient.Reply.Ack;
});
```

こちらも実験用のスイッチとログは省いています。

この`done`判定で処理を省略できるのは、完了後に同じ注文が届いた場合です。
同じ注文を複数ワーカーが同時に処理する場合や、外部APIの呼び出し後に落ちた場合は、排他制御や注文IDによる処理先での重複防止も必要です。
`process_count`はDBの更新回数なので、重い処理そのものの実行回数とはずれることがあります。

https://docs.cloud.google.com/pubsub/docs/subscribe-best-practices?hl=ja

ロールバックで注文が保存されなかった場合も、ワーカーはnackを返し続けます。
処理できないメッセージの転送先として、dead letterトピックを設定できます。

https://docs.cloud.google.com/pubsub/docs/dead-letter-topics?hl=ja

## ackする前に落ちたとき
重い処理が終わってDBを`done`にしたあと、ackを返す前にワーカーを落としてみました。

```text
22:15:41.789 update order=1 affected=1
22:15:41.789 crash before ack
22:16:50.778 recv msgId=22615588617033601 order=1 publishTime=22:15:38.713
22:16:50.853 ack (already done)
```

再起動したワーカーに同じメッセージが届いて、`done`なので何もせずackしています。
`process_count`は1のままでした。
状態を見ずに毎回処理するワーカーで同じことをすると、`process_count`が2になりました。

再送は、ワーカーを落としてから約69秒後でした。

C#の`SubscriberClient`は、メッセージの処理を始める前に`HandleExtendLease`を呼んでいます。

```c#:SubscriberClientImpl.SingleChannel.cs
HandleExtendLease(msgIds, null);
Task messagesTask = _taskHelper.Run(
    () => ProcessPullMessagesAsync(msgs, msgIds));
```

https://github.com/googleapis/google-cloud-dotnet/blob/76d4c27ae2cfece6774bdebfaf2f944b9c2847e7/apis/Google.Cloud.PubSub.V1/Google.Cloud.PubSub.V1/SubscriberClientImpl.SingleChannel.cs#L501-L509

期限を延長するときは、以下のようにSDKから`ModifyAckDeadlineAsync`を呼びます。

```c#:SubscriberClientImpl.SingleChannel.cs
Task extendTask = _client.ModifyAckDeadlineAsync(
    _subscriptionName,
    extends.Select(x => x.Id),
    EffectiveLeaseTiming.AckDeadlineSeconds,
    _handlerCts.Token);
```

https://github.com/googleapis/google-cloud-dotnet/blob/76d4c27ae2cfece6774bdebfaf2f944b9c2847e7/apis/Google.Cloud.PubSub.V1/Google.Cloud.PubSub.V1/SubscriberClientImpl.SingleChannel.cs#L808-L813

`SubscriberClient.DefaultAckDeadline`の既定値は60秒です。
起動時に出してみても`00:01:00`でした。
サブスクリプションに設定した10秒とは別に、このSDKが使うack期限があります。
今回の約69秒には、この期限と再試行の待ち時間が影響したと考えています。

https://docs.cloud.google.com/dotnet/docs/reference/Google.Cloud.PubSub.V1/latest/Google.Cloud.PubSub.V1.SubscriberClient#Google_Cloud_PubSub_V1_SubscriberClient_DefaultAckDeadline

自動延長が続く時間の上限は、`MaxTotalAckExtension`で設定できます。
ハンドラから戻らないまま3分待つ検証では、再送されませんでした。

https://docs.cloud.google.com/dotnet/docs/reference/Google.Cloud.PubSub.V1/latest/Google.Cloud.PubSub.V1.SubscriberClient.Settings#Google_Cloud_PubSub_V1_SubscriberClient_Settings_MaxTotalAckExtension

ライブラリのack期限を20秒にして落とすと約31秒、120秒にすると約133秒で再送されました。
どちらも、設定した期限より十数秒後に再送されています。

# ワーカーに分ける利点
重い処理を別サーバーのワーカーに任せることで、APIサーバーのCPUやメモリの負荷を軽減できます。

同じサブスクリプションに複数のワーカーを接続すれば、メッセージを分担して処理できます。
処理量が増えたときに、ワーカー側のサーバーを増やす構成にできます。

https://docs.cloud.google.com/pubsub/docs/pubsub-basics?hl=ja

APIは受付結果を先に返せるので、呼び出し元も重い処理の完了まで待たずに済みます。

![sync-async.png](https://qiita-image-store.s3.ap-northeast-1.amazonaws.com/0/855584/32511c68-e4f6-4e56-8fdd-03b4ff237025.png)

# お値段とお片付け
Pub/Subの送受信量の料金は、publishと受け取りのバイト数で決まります。

今回のpull構成では、送受信量の合計が請求先アカウントごとに月10GiBまでは無料で、1リクエストあたり最低1KBで数えられます。
ほかに、メッセージの保存や通信経路に応じたデータ転送料金もあります。

https://cloud.google.com/pubsub/pricing?hl=ja

**ackされないメッセージをサブスクリプションに1日以上残すとストレージ料金がかかる**ので、終わったら消しておきます。

```console
$ gcloud pubsub subscriptions delete pubsub-lab-orders-worker
Deleted subscription [projects/<PROJECT>/subscriptions/pubsub-lab-orders-worker].
$ gcloud pubsub topics delete pubsub-lab-orders
Deleted topic [projects/<PROJECT>/topics/pubsub-lab-orders].
```

# まとめ
publishのタイミングや再送時の処理は考えておく必要がありますが、重い処理をワーカーに分けられる便利なサービスなのでガンガン使っていきたいですね。

# 参考
https://docs.cloud.google.com/pubsub/docs/subscription-properties?hl=ja

https://docs.cloud.google.com/pubsub/docs/lease-management?hl=ja

https://cloud.google.com/pubsub/pricing?hl=ja
