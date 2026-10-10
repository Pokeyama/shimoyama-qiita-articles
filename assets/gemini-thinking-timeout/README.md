# Qiita用の図

| PNG | 内容 | 編集元 |
| --- | --- | --- |
| `portal-infrastructure.png` | ポータルの全体構成。今回の同期に関わる外部API接続を青線で強調 | `portal-infrastructure.drawio` |
| `chatwork-sync-infrastructure.png` | 今回のお知らせ同期に絞った構成。障害当時の60秒タイムアウトと再試行 | `chatwork-sync-infrastructure.drawio` |
| `timeout-flow.png` | タイムアウトした回と次回の再試行を示すシーケンス図 | `timeout-flow.drawio` |

PNGをQiitaにアップロードして使用する。drawioファイルは公式アイコンを埋め込み済み。

全体図はポータルの`docker-compose.yml`、README、`src/lib/`の外部API連携に基づく。外部サービスとの矢印はAPIの呼び出し先を表す。Google連携・Web Pushには設定が必要。

`reminder`は自作の定期実行ワーカー。`app`と同じイメージを使い、別コンテナで`node scripts/reminder-worker.mjs`を実行する。DockerやNext.jsの標準サービスではない。

アイコンの配布元は`icons/SOURCES.md`に記載。
