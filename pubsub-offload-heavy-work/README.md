# Pub/Subで重い処理を非同期に逃がす検証

```sh
docker compose up -d
export GOOGLE_CLOUD_PROJECT=$(gcloud config get-value project)
dotnet run -- worker   # 別ターミナル
dotnet run -- api      # 別ターミナル
./bench.sh sync        # または async。第2引数で回数(デフォルト5)
```

認証はADC（`gcloud auth application-default login`）。トピック`pubsub-lab-orders`とサブスクリプション`pubsub-lab-orders-worker`は事前に作っておく。

## 環境変数

| 変数 | デフォルト | 対象 | 用途 |
|---|---|---|---|
| `GOOGLE_CLOUD_PROJECT` | なし（必須） | 共通 | プロジェクトID |
| `TOPIC` | `pubsub-lab-orders` | api | 送信先トピック |
| `SUBSCRIPTION` | `pubsub-lab-orders-worker` | worker | 受信するサブスクリプション |
| `DB` | `Server=127.0.0.1;Port=3308;User=root;Password=pass;Database=lab` | 共通 | 接続文字列 |
| `HEAVY_MS` | `3000` | 共通 | 重い処理の代わりの待ち時間(ms) |
| `FLOW` | `publish-then-commit` | api | `publish-then-commit` / `commit-then-publish` |
| `DELAY_BEFORE_COMMIT_MS` | `0` | api | publish後、COMMIT前の待ち(ms) |
| `FAIL_COMMIT` | `0` | api | `1`でpublish後にROLLBACKして500 |
| `CRASH_AFTER_COMMIT` | `0` | api | `1`でCOMMIT後、publish前に落ちる |
| `WORKER_MODE` | `normal` | worker | `normal` / `naive` / `nack` / `hang` |
| `WORKER_CONCURRENCY` | `1` | worker | 同時処理数（`MaxOutstandingElementCount`） |
| `CRASH_BEFORE_ACK` | `0` | worker | `1`でUPDATE後、ack前に落ちる |
| `RECHECK_MS` | `200` | worker | 0超なら行なし時にこの時間待って1回だけ再SELECT(normalのみ) |
| `ACK_IF_MISSING` | `0` | worker | `1`で行なし時にnackせずack(取りこぼしの再現用、normalのみ) |
| `ACK_DEADLINE_SEC` | 未設定(ライブラリ既定) | worker | `SubscriberClient.Settings.AckDeadline`(秒) |
