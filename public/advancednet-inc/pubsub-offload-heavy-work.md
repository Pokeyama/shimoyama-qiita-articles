---
title: Pub/Subで重い処理を非同期に逃がそう
tags:
  - PubSub
  - GoogleCloud
  - C#
  - MySQL
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
APIの中で重い処理をすると、その分だけレスポンスが遅くなります。
結果を待たなくていい処理なら、Pub/Subに投げて別のプログラムにやらせればAPIはすぐ返せます。
この記事では、Pub/Subに処理を逃がす手順と、実際に動かして分かった受け手の書き方をまとめます。

送り手と受け手はC#、DBはMySQLで、受け手はpullで受け取ります。

# 環境
macOS 26.5.2
.NET SDK 10.0.300
Google.Cloud.PubSub.V1 3.39.0
MySqlConnector 3.0.0
MySQL 8.4 (Docker)

# Pub/Subとは
GCPのメッセージングサービスです。
送り手がトピックにメッセージをpublishすると、そのトピックにつながっているサブスクリプションごとに1通ずつ届きます。

受け手はサブスクリプションからメッセージを受け取って、処理が終わったらackを返します。
ackが返ってくるまでメッセージは消えず、ack期限を過ぎると再送されます。

処理できなかったときはnackを返すと、ack期限を待たずに再送の対象になります。

受け取り方は、受け手から取りに行くpullと、Pub/Subが受け手のURLを呼ぶpushの2つがあって、この記事はpullです。

あと、**同じメッセージが2回届くことがあります。**

AWSで言うと以下です。

| Pub/Sub | AWS |
| --- | --- |
| トピック | SNSのトピック |
| サブスクリプション | SQSのキュー |
| ack期限 デフォルト10秒 | SQSの可視性タイムアウト デフォルト30秒 |
| nack | 可視性タイムアウトを0にする |
| dead letterトピック | SQSのデッドレターキュー |
| 未ackのメッセージの保持期間 デフォルト7日・最長31日 | SQSのメッセージ保持期間 デフォルト4日・最長14日 |

https://docs.aws.amazon.com/ja_jp/sns/latest/dg/sns-sqs-as-subscriber.html

https://docs.aws.amazon.com/ja_jp/AWSSimpleQueueService/latest/SQSDeveloperGuide/sqs-visibility-timeout.html

# 構成
<!-- 画像: API（送り手）→ Pub/Sub（トピック → サブスクリプション）→ 受け手 → MySQLの構成図。APIと受け手は同じMySQLを見る -->

APIは注文を受けたら、注文を`pending`で保存して、Pub/Subに注文IDをpublishしてすぐ返します。

受け手は、メッセージの注文IDでDBを見て、重い処理をして`done`にします。
受け手はPub/Subが動かしてくれるものではなく、自分で書いて動かすプログラムです。

テーブルは以下のような感じです。

```sql:init.sql
CREATE TABLE orders (
  order_id BIGINT AUTO_INCREMENT PRIMARY KEY,
  status VARCHAR(16) NOT NULL,          -- 'pending' / 'done'
  process_count INT NOT NULL DEFAULT 0, -- 二重処理の検出用
  created_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  done_at DATETIME(3) NULL
);
```

重い処理は3秒待つだけにしています。

比較用に、同じ重い処理をAPIの中でやる`/orders/sync`も作っておきます。

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

受け手がnackを返し続けると、すぐに再試行ではその間ずっと再送が回り続けます。

なので最短10秒・最長600秒にしています。

実際にnackを返し続ける受け手を40分動かして、再送の間隔を見てみました。

```text
14.6, 16.9, 20.0, 22.0, 23.0, 30.0, 37.1, 43.1, 79.0, 88.1, 126.1, 150.1, 159.2, 236.1, 263.3, 512.4, 567.6（秒）
```

倍々ではなく、ばらつきながら伸びていきます。
最短10秒の設定でも、2秒かからずに再送された回がありました。

# 送り手を書く
`PublisherClient`は起動時に1回だけ作って、使い回します。
エンドポイントは以下のようになります。
実験用のスイッチとログは省いています。

```c#:Program.cs（送り手の抜粋）
var publisher = await new PublisherClientBuilder
{
    TopicName = TopicName.FromProjectTopic(project, "pubsub-lab-orders"),
}.BuildAsync();

app.MapPost("/orders/async", async () =>
{
    await using var conn = new MySqlConnection(db);
    await conn.OpenAsync();
    var tx = await conn.BeginTransactionAsync();
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
なので「コミットしてからpublish」と「publishしてからコミット」のどちらかを選ぶことになります。

## コミットしてからpublishすると取りこぼす
コミットとpublishの間でプロセスが落ちると、DBには保存されているのにメッセージが送られません。
間でわざと落としてみると以下のようになりました。

```text
22:02:47.530 insert order=1
22:02:47.533 commit order=1
22:02:47.533 crash after commit
```

60秒待っても注文は`pending`のままで、受け手には1通も来ませんでした。

## publishしてからコミットする
逆の順番なら、publishが失敗すると例外でコミットまでたどり着かないので、保存されたのにメッセージが無い状態にはなりません。

代わりに、メッセージだけ届いてDBには無い、という状態が起きます。
これは受け手で吸収します。

送るメッセージをいったんテーブルに書いて、別のプロセスがpublishするOutboxパターンというやり方もあります。
今回は受け手が同じDBを見られるので、そこまではしていません。

# 受け手を書く
受け手は以下のルールで書きます。

1. 注文が`pending` → 重い処理をして`done`にしてack
1. 注文が`done` → 何もしないでack
1. 注文が見つからない → 200ms待ってもう一度見る。それでも無ければnack

```c#:Program.cs（受け手の抜粋）
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

`FlowControlSettings`の1は、同時に処理するメッセージの数です。
同期版と比べるときに条件を揃えたいので1にしています。

## コミットより先に届く
最初は見直しを入れず、見つからなければすぐnackにしていました。
送り手と受け手のログを時刻順に並べると以下のようになっていました。

```text
22:09:25.364 [送り手] insert order=1
22:09:25.539 [受け手] recv msgId=23441922620419938 order=1 publishTime=22:09:25.555
22:09:25.544 [受け手] nack (row not found)
22:09:25.548 [送り手] published order=1 msgId=23441922620419938
22:09:25.553 [送り手] commit order=1
```

**送り手の`PublishAsync`が返るより先に、受け手が受け取っています。**

コミットより先に届くのはたまにだと思っていたら、受け手が暇なときはだいたいこれでした。
5件投げたら5件ともnackで、全部10秒くらい待たされていました。

なので、見つからなかったら200msだけ待ってもう一度見るようにしています。
コミットを0.1秒遅らせて試すと以下のようになりました。

```text
22:54:08.616 published order=1 msgId=22615536250928311
22:54:08.623 recv msgId=22615536250928311 order=1 publishTime=22:54:08.614
22:54:08.722 commit order=1
22:54:08.876 recheck order=1 found=True
22:54:11.884 update order=1 affected=1
22:54:11.884 ack (processed)
22:54:13.470 recv msgId=22615536250928311 order=1 publishTime=22:54:08.614
22:54:13.476 ack (already done)
```

見直しで見つかって、nackせずに処理できています。

そしてackしたのに同じメッセージがもう1回来てる。
`done`なら何もしないでackするようにしているので、`process_count`は1のままです。

コミットを1秒遅らせたときは、見直しでも見つからずにnackになって、再送で処理されました。
見直しは保険で、間に合わなければ今まで通りnackに落ちます。

## 見つからないときにackすると取りこぼす
見つからないときにackしてしまうと、あとでコミットされた注文を取りこぼします。

```text
22:37:43.342 published order=1 msgId=23309967033347292
22:37:43.905 recv msgId=23309967033347292 order=1 publishTime=22:37:43.336
22:37:43.964 ack (row not found)
22:37:48.351 commit order=1
```

65秒待っても注文は`pending`のままで、メッセージは二度と来ませんでした。

なので見つからないときは必ずnack。

ロールバックで本当に保存されなかった場合は、受け手がnackを返し続けることになります。
コミットだけ失敗させてみると、11.5秒、15.0秒、15.0秒、25.0秒、27.0秒と間隔を伸ばしながらnackが続きました。

保持期間の間はこのままなので、気になる場合はdead letterトピックで一定回数で逃がすことになります。

https://docs.cloud.google.com/pubsub/docs/dead-letter-topics?hl=ja

## ackする前に落ちたとき
重い処理が終わってDBを`done`にしたあと、ackを返す前に受け手を落としてみました。

```text
22:15:41.789 update order=1 affected=1
22:15:41.789 crash before ack
22:16:50.778 recv msgId=22615588617033601 order=1 publishTime=22:15:38.713
22:16:50.853 ack (already done)
```

再起動した受け手に同じメッセージが届いて、`done`なので何もせずackしています。
`process_count`は1のままでした。
状態を見ずに毎回処理する受け手で同じことをすると、`process_count`が2になりました。

再送まで69秒かかっているのは、C#のライブラリがack期限を自分で延ばしているからです。

起動時に`SubscriberClient.DefaultAckDeadline`を出してみると`00:01:00`で、ライブラリ側のack期限は60秒でした。
受け手が処理している間は、期限が来る前にライブラリが延長してくれます。

> デフォルトでは、クライアント ライブラリは定期的な modifyAckDeadline リクエストを発行することで期限を 1 時間延長できます。

https://docs.cloud.google.com/pubsub/docs/lease-management?hl=ja

ハンドラから戻らないまま3分待っても、再送はされませんでした。
サブスクリプションのack期限は10秒にしていますが、受け手が生きている間は関係ないです。

ライブラリのack期限を20秒にして落とすと約31秒、120秒にすると約133秒で再送されました。

# 逃がす前と後で比べる
同じ3秒の重い処理を、APIの中でやる場合とPub/Subに逃がす場合で比べてみます。
注文を5件、順番に投げて、APIの応答時間と全部`done`になるまでの時間を見ます。
全部終わるまでの時間は、DBの`created_at`の最小から`done_at`の最大までで見ています。

| | APIの応答（5件合計） | 全部終わるまで | nack |
| --- | --- | --- | --- |
| APIの中で処理 | 15.06秒 | 15.11秒 | - |
| Pub/Sub・見直しなし | 0.86 / 0.85 / 0.53秒 | 18.57 / 30.13 / 19.39秒 | 0 / 5 / 2回 |
| Pub/Sub・見直しあり | 0.81 / 0.75 / 0.52秒 | 18.51 / 18.88 / 18.80秒 | 0 / 0 / 0回 |

Pub/Subのほうは3回ずつ回しています。（同期は1回だけ）

APIの応答は15秒から1秒以下になりました。

ですが全部終わるまでの時間は短くなっていなくて、むしろ3〜4秒延びています。
受け手のログを見ると、ackしてから次を受け取るまで0.2〜1.2秒くらい空いていました。
同時に処理する数を1にしているので、その分がそのまま積み上がっています。

見直しなしの30秒の回は、5件ともnackして10秒くらい待たされていました。
1件目がnackですぐ手が空いて、2件目以降もコミット前に取りに行っていたっぽい。


# 逃がしていい処理
処理の重さは変わらず、APIが待たなくなるだけです。
結果をレスポンスで返したい処理は逃がしても待つことになるので、逃がすのは待たなくていい処理とやり直せる処理だけです。

# お値段とお片付け
Pub/Subはpublishと受け取りのバイト数で課金されます。

請求先アカウントごとに月10GiBまでは無料で、1リクエストあたり最低1KBで数えられます。

https://cloud.google.com/pubsub/pricing?hl=ja

**ackされないメッセージをサブスクリプションに1日以上残すとストレージ料金がかかる**ので、終わったら消しておきます。

```console
$ gcloud pubsub subscriptions delete pubsub-lab-orders-worker
Deleted subscription [projects/<PROJECT>/subscriptions/pubsub-lab-orders-worker].
$ gcloud pubsub topics delete pubsub-lab-orders
Deleted topic [projects/<PROJECT>/topics/pubsub-lab-orders].
```

# まとめ
Pub/Subに逃がすのは簡単でも、2回届くことと、コミット前に届くことは受け手で受け止めないといけないという記事でした。
見直しの200msは勘で決めたので、もうちょっとまともな決め方がありそうです。

# 参考
https://docs.cloud.google.com/pubsub/docs/subscription-properties?hl=ja

https://docs.cloud.google.com/pubsub/docs/lease-management?hl=ja

https://cloud.google.com/pubsub/pricing?hl=ja
