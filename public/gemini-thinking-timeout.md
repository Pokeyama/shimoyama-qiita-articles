---
title: 文章の要約でクラウド破産しかけました
tags:
  - Gemini
  - VertexAI
  - GoogleCloud
private: false
updated_at: '2026-10-05T12:28:26+09:00'
id: bc3dd1d2109568834a02
organization_url_name: advancednet-inc
slide: false
ignorePublish: false
---

# はじめに

弊社では社内ポータルサイトを自前で用意して運用しています。
Chatworkの業務連絡をGeminiで要約して載せる処理を入れているんですが、ここでやらかしました。

**同じ文章の要約に45回失敗し、11時間リトライを繰り返して832円かかっていました。**
アプリ側は60秒でタイムアウトしていたのに、Google側では数分かけて生成が続き、その分も課金されていました。

思考量とリトライ回数に上限をつけていなかったのがまずかったです。
最後の1回だけ要約に成功したことで止まりましたが、成功した理由はわかっていません。
成功しなければ課金が増え続けているとこでした。

最終的な金額は大したことないんですが、**1回1円もかからない想定だった部分だったので**下手するとクラウド破産しているところでした。
失敗エントリです。

# 環境

- さくらVPS、Debian 13
- Docker Compose
- Next.js 16、Node.js 22、TypeScript
- PostgreSQL 17、Prisma
- Vertex AI、`asia-northeast1`
- モデルは`gemini-2.5-flash`

# ポータルの構成

弊社では申請や社内情報などを統括するポータルサイトを自前で用意して運用しています。
さくらVPS上で動かしており、Google関連のサービスを使用するためにGCPと連携しています。

![portal-infrastructure.png](https://qiita-image-store.s3.ap-northeast-1.amazonaws.com/0/855584/8295e221-40bb-4211-808f-705f8ae80997.png)


その中に「社内Chatworkから業務連絡を取得して**要約してから**ポータルに流す」という処理を入れています。
![chatwork-sync-infrastructure.png](https://qiita-image-store.s3.ap-northeast-1.amazonaws.com/0/855584/a6b73632-435b-4727-bb36-3bbf9311cd4d.png)


定期的にChatworkからクロールしており、`reminder`というWorkerを用意しています。
中身はNode.jsのスクリプトです。

今回はこのGeminiで要約している部分が想像以上に思考しており、思考・出力の量とリトライ回数に上限を設定していなかったため、余計な課金が発生していました。

# 当時の処理

Workerから15分ごとに、ポータルの業務連絡同期APIを叩いていました。
Chatworkの直近100件を取得し、最新5件のうち未処理の発言を古い順に処理していきます。

発言は1件ずつGeminiに渡します。
全社員向けのお知らせかどうかを判定し、タイトル・要約・重要度・表示期限をJSONで返してもらっています。

要約に失敗したら次の文章に進めず、次の定期実行で同じ発言の要約をリトライする作りでした。
**やり直し回数の上限は設定していませんでした。**

生成の設定は以下のような感じです（スキーマの中身は省略）。

```json
{
  "generationConfig": {
    "temperature": 0,
    "responseMimeType": "application/json"
  }
}
```

実際には`responseSchema`も指定しています。
`thinkingConfig`と`maxOutputTokens`は指定していませんでした。
Gemini 2.5 Flashは思考を使用するモデルで、思考の量を自分で指定しない設定のまま使っていました。

https://docs.cloud.google.com/gemini-enterprise-agent-platform/models/thinking?hl=ja

Chatworkの取得は30秒、Geminiの呼び出しは`AbortSignal.timeout(60_000)`で切るようにしていました。

# 1件の発言で11時間止まっていた

今回引っかかったのは、パーティに関する約600文字の連絡です。

<details>
<summary>実際に処理した文章（だいぶ省略してます）</summary>

> 各位
>
> お疲れ様です。
> 社内行事について早速のご回答ありがとうございます。
>
> まだアンケート期間中ではございますが、いくつかお問い合わせいただいた件についてご連絡させていただきます。
>
> ◆キッズメニューについて
> お子様向けメニューについてのなんやかんや
>
> ◆席順について
> 席順についてのなんやかんや
>
> ◆ドリンクについて
> ドリンクについてのなんやかんや
>
> ◆アレルギーありとご回答の方
> アレルギーについてのなんやかんや
>
> アンケート終了後、参加者が最終確定した後にパーティ専用窓をご用意しますので
> そのタイミングまではこちらの窓で失礼致します。
> 何かございましたら［担当者A］までよろしくお願いします。

</details>


この連絡がChatworkに投稿されてから、同期が15分おきに失敗していました。
以下はワーカーのログから、最初と最後の失敗、その次の成功を抜粋したものです。

```text
2026-10-02T04:00:53Z Chatwork sync failed (500, application/json)
2026-10-02T15:00:54Z Chatwork sync failed (500, application/json)
2026-10-02T15:15:01Z Chatwork sync completed:
{"configured":true,"fetched":100,"processed":1,"created":1,"skipped":0}
```

ログの時刻はUTCです。
日本時間では13:00ごろから翌日の0:00ごろまで45回失敗し、0:15の要約だけ成功していました。

前述していますが、失敗しても次の発言に移るのではなく、次の定期実行（15分ごと）で再試行という処理にしていました。

アプリ側には毎回、以下のエラーが出ていました。

```text
chatwork-sync: Error [TimeoutError]: The operation was aborted due to timeout
```

同じ時間帯にChatworkAPIを使用した重めのテストで毎分Chatworkを呼んでいたので、こっちが詰まらせているのかと思いました。
ですが、アプリ側から直接ChatworkAPIを叩いても普通に返ってきたので特に処理負荷的なことは問題なさそうでした。

同期の開始時刻と失敗時刻を見ると、毎回きっかり60秒でした。
Chatworkなら前述した通り30秒で切れるので、Geminiの呼び出しでタイムアウトしていると当たりをつけました。

# Google側は200で終わっていた

次にGCPコンソールの「APIとサービス」から、`aiplatform.googleapis.com`の指標を見ていきます。

呼び出しに使用しているサービスアカウントで絞ると、15分おきにリクエストが並んでいました。
レスポンスコードは全部200で、`GenerateContent`のエラー数は0でした。



アプリでタイムアウトしているのに、Google側では普通に終わってるっぽい。

レイテンシのグラフでは、ほとんどの回が約3.5〜4.5分、一部が約2分でした。

0:15の成功した回だけ数秒で、アプリのログでも約7秒で終わっています。

![スクリーンショット 2026-10-04 1.37.36.png](https://qiita-image-store.s3.ap-northeast-1.amazonaws.com/0/855584/d3cd203c-9e27-4d58-a3ba-1bc2301cf903.png)

今回のログと指標から見ると、以下のような流れだったと考えられます。

![timeout-flow.png](https://qiita-image-store.s3.ap-northeast-1.amazonaws.com/0/855584/23fe08e9-0b42-4fae-b01c-aacf474c5f84.png)

ポータル側が60秒で待つのをやめても、Google側では生成が終わっていませんでした。
待つのをやめただけで、生成のキャンセルにはなっていなかったようです。

料金ページには、レスポンスコード200のリクエストが課金対象と書かれています。

ポータルの500だけ見ていても、こっちで成功していることには気づけませんでした。
悲しい。

https://cloud.google.com/gemini-enterprise-agent-platform/generative-ai/pricing?hl=ja

# ほぼ思考の出力に課金されていた

最終的な請求レポートで832円でした。
SKUは全てGemini2.5です。

```text
Gemini 2.5 Flash GA Thinking Text Output - Predictions
```

お知らせの短いJSONを作るだけなのに、ほぼ思考の出力に課金されていました。

思考のトークンも課金対象なので、返ってくるJSONの短さはあまり関係なかったです。

https://docs.cloud.google.com/gemini-enterprise-agent-platform/models/thinking?hl=ja

失敗45回と成功1回で46回呼んでいるので、単純に割ると1回約18円です。

1回1円未満のつもりだったんですが。。。

# なぜこの発言だけ長く考えたか

思考の中身は見られないので推測でしかないのですが、

- **お礼なのか、お知らせなのかの判定で迷った？** 前置きは「ご回答ありがとうございます」ですが、その後は全社員向けの連絡です。お礼・返信はお知らせにしないという指示と、全社員向けの連絡を拾う指示がぶつかったのかもしれません。
- **複数の話題を80文字以内にまとめるのが難しかった？** キッズメニュー・席順・ドリンク・アレルギーの話が1つの発言に入っています。どの内容を残して短くするかで、長く考えた可能性もあります。
- **プロンプトの日付が変わった影響？** 表示期限を決めるために今日の日付を渡しています。0:00の回は前日の23:59に始まったので10/2、成功した0:15の回は10/3でした。ただ、原文には日付がなく、成功した下書きも既定の「投稿日から14日後」になっています。日付の計算で迷ったという予想は弱そうで、偶然通った可能性もあります。

レスポンスを受け取れていないので、実際の思考トークン数も手元にはないです。
`temperature: 0`でも、失敗した回のレイテンシは約2分〜4.5分とばらついていました。
日付を固定した再現実験はしていないので、今のところ原因はわかっていません。

# 対策

## 思考と出力に上限をつける

生成の設定に、以下の2つを追加しました。

```json
{
  "thinkingConfig": {
    "thinkingBudget": 1024
  },
  "maxOutputTokens": 4096
}
```

`thinkingBudget`で思考に使うトークンの予算を指定します。
公式ドキュメントでも、実際の思考トークン数は指定した値と若干異なる場合があるとされています。

https://docs.cloud.google.com/gemini-enterprise-agent-platform/models/thinking?hl=ja

`maxOutputTokens`は思考を含む生成トークン全体の上限です。
思考の予算と、出力全体の上限を両方指定しておきます。

https://ai.google.dev/gemini-api/docs/thinking?hl=ja

上限に達したときは、出力が途中で切れたり空になったりする可能性があります。

JSONをパースできなかったり、必要なフィールドが欠けたりした場合は判定の失敗として数えます。

## 同じ発言の失敗は3回まで

同じ発言で3回続けて判定に失敗したら、カーソルを進めるようにしました。

1件の失敗で、後ろの発言まで止まり続けるのを防ぎます。

失敗回数は発言IDをキーにした`Map`で数えています。
再起動でリセットされ、複数プロセスでは共有されません。

タイムアウトしたらまたやる、という作りだったのでここも止めてあげます。

## 飛ばした発言は管理画面に残す

最初は、3回失敗したらログを出して飛ばすだけにしました。
ですがこれだと、サーバーのログを見ないと気づけません。

飛ばした発言は、`【AI判定失敗】`で始まるタイトルの非公開の下書きにしました。
要約できなかった原文とChatworkへのリンクを残します。

管理画面で確認し、必要なら手で直して公開する形です。

下書きの作成とカーソルの更新は、同じトランザクションで行うようにしています。

# まとめ

従量課金のLLM APIを利用するときは、生成の量とやり直し回数にも上限をつけておきましょう。
あーこわいこわい。

# 参考

https://docs.cloud.google.com/gemini-enterprise-agent-platform/models/thinking?hl=ja

https://cloud.google.com/gemini-enterprise-agent-platform/generative-ai/pricing?hl=ja

https://ai.google.dev/gemini-api/docs/thinking?hl=ja
