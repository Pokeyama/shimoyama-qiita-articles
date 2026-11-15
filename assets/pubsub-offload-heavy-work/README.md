# Pub/Subの用語説明用の図

| 編集元 | 記事用画像 | 挿入位置 |
| --- | --- | --- |
| [pubsub-basics.drawio](pubsub-basics.drawio) | [pubsub-basics.png](pubsub-basics.png) | 「Pub/Subとは」の冒頭 |
| [order-flow.drawio](order-flow.drawio) | [order-flow.png](order-flow.png) | 「構成」 |
| [commit-then-publish.drawio](commit-then-publish.drawio) | [commit-then-publish.png](commit-then-publish.png) | 「コミットしてからpublishすると取りこぼす」 |
| [publish-then-commit.drawio](publish-then-commit.drawio) | [publish-then-commit.png](publish-then-commit.png) | 「publishしてからコミットする」 |
| [sync-async.drawio](sync-async.drawio) | [sync-async.png](sync-async.png) | 「ワーカーに分ける利点」 |

用語図の2つのサブスクリプションは、複数のサブスクリプションへの配信を示す説明用の例です。
検証で使用した構成は注文処理用のサブスクリプション1つで、構成図はこちらを示しています。
構成図はAPIとワーカーを別サーバーに分ける構成を示しています。
動作確認では同じMac上の別プロセスで実行しています。
同期・非同期の図とCOMMIT／publishの比較図は、処理の順序を示す模式図です。
縦方向の間隔は実測時間を表していません。
COMMITとpublishの比較図は、API・MySQL・Pub/Sub・ワーカーの配置を揃えています。
`commit-then-publish`の破線は、停止によって実行されなかったpublishです。
`publish-then-commit`は、コミット前に受信してnackを返し、その後コミット・再送される例です。

`build_diagram.py`でdrawioを生成し、draw.io DesktopでPNGを書き出しています。
各図形・ラベル・矢印はdraw.ioで個別に編集できます。

## 出典

- 用語と配信の関係：https://docs.cloud.google.com/pubsub/docs/pubsub-basics?hl=ja
- pull：https://docs.cloud.google.com/pubsub/docs/pull?hl=ja
- push：https://docs.cloud.google.com/pubsub/docs/push?hl=ja
- Pub/Sub公式アイコン：https://cloud.google.com/icons
- アイコンの配布元：https://services.google.com/fh/files/misc/google-cloud-legacy-icons.zip の `pubsub/pubsub.svg`

Pub/Subの公式アイコンは色と縦横比を維持し、drawioへ埋め込んでいます。
MySQLロゴは公式配布元（https://www.mysql.com/common/logos/logo-mysql-170x115.png）から取得済みの素材を流用しています。

サーバー・歯車・封筒・分岐・受け取り口・人のアイコンはGoogle Material Iconsです。
特定の実行環境を示す製品アイコンではありません。

- 配布元：https://github.com/google/material-design-icons
- 元ファイル：`src/device/storage`、`src/action/settings`、`src/communication/mail_outline`、`src/hardware/device_hub`、`src/content/inbox`、`src/social/person` の `materialicons/24px.svg`
- ライセンス：Apache-2.0（`icons/LICENSE.material-icons`）

Material Iconsはdrawioへの埋め込み時に配色しています。
