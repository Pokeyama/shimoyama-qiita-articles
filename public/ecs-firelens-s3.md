---
title: 【ECS】FireLensでログをCloudWatchからS3へ逃がす
tags:
  - AWS
  - ECS
  - Fargate
  - FireLens
  - Terraform
private: false
updated_at: ''
id: null
organization_url_name: advancednet-inc
slide: false
ignorePublish: false
---

# はじめに
CloudWatch高いですよねー
CloudWatch Logsは取り込んだ量で課金されるので、入れた時点で課金が発生します。

でもECSをそのまま構築するとデフォルトでCloudWatchに送られてしまいます。
ということでFireLensをサイドカーとして追加して、nginxのアクセスログをS3に逃がすことで、エラー行だけCloudWatchに残して必要最低限な情報だけCloudWatchで見るようにしたいと思います。
S3のほうは保管量とPUT回数で、取り込みは無料です。
Datadogのような外部SaaSへ送るところは書きません。

# 環境
・AWS ap-northeast-1
・ECS on Fargate（cpu 256 / memory 512）
・nginx `public.ecr.aws/nginx/nginx:1-alpine`
・fluent-bit `public.ecr.aws/aws-observability/aws-for-fluent-bit:init-latest`
・Terraform v1.15.5
・AWS CLI 2.36.42

fluent-bitのバージョンはFluent Bit v1.9.10でした。
`init-latest`なのに3系ではないのはなぜなのか、そこは追っていないのでわかってないです。

# FireLens
AWS公式のタスク定義パラメータのドキュメントは、一度は開いたことがあるのではないでしょうか。
`logDriver`のValid valuesを8個並べた直後に、こう書いてあります。

> The supported log drivers are `awslogs`, `splunk`, and `awsfirelens`.

EC2起動タイプなら`fluentd`も`json-file`も`syslog`も使えますが、Fargateはこの3つだけです。
そしてDatadogのような外部SaaS用のログドライバは存在しません。
Datadog等に送りたい場合も、FireLensを経由することになります。

FireLensを使わずにCloudWatchの外へ出す方法もありますが、どれも微妙です。
`awslogs`でCloudWatchに入れてからサブスクリプションフィルタとLambdaで転送する形は、取り込み料金が発生してしまうので安くしたいという話と噛み合いません。
アプリから直接HTTPで送る形はログライブラリがベンダー依存になり、stdoutに出なくなるのでローカルでの挙動も変わります。
Datadog Agentをサイドカーで同居させる形は、サイドカーが増えるところが結局同じです。（そもそもDataDogが高い）

# 構成
`awsfirelens`という名前のログドライバが実際にあるわけではありません。
タスク定義にこう書いておくと、ECSがアプリコンテナのログ送信先をサイドカーへ向ける設定に書き換えてくれます。

> The FireLens container receives application logs over a UNIX socket.
> FireLens listens on port `24224`

24224はfluentdのforwardプロトコルの標準ポートです。

:::note warn
ポート24224はセキュリティグループで開けないこと、と公式に注意書きがあります。
開けるとタスクの外からログを流し込めてしまいます。
:::

最終的にできあがるログの流れが以下のような感じです。

![nginxのログがfluent-bit経由でS3へ流れ、エラー行だけCloudWatch Logsにも複製される構成図](https://qiita-image-store.s3.ap-northeast-1.amazonaws.com/0/855584/58d31f94-ad04-4a7f-9901-eadcb123530f.png)

nginxはstdoutに出すだけで、アプリのコードは1行も変えません。
イメージも`public.ecr.aws/nginx/nginx:1-alpine`をそのまま使っていて、nginx側の設定は何も足していません。

プラットフォーム側でやってくれるのは4つです。

1. 同じホストへの同時配置
1. ネットワーク名前空間の共有
1. ボリュームの共有
1. ライフサイクルの連動

`awsvpc`モードなので2つのコンテナが1つのENIとパブリックIPを共有していて、k8sのPodと同じ構造になっています。
Podのほうは以前ローカルで試したときに書きました。

https://qiita.com/simoyama2323/items/77bd8dfbaba10026905b

# タスク定義を書く
アプリ側のコンテナはログドライバを`awsfirelens`にして、optionsに送り先を書いていきます。
ここに書いた値がそのままfluent-bitのS3出力の設定になります。
ロールとバケットのTerraformは省略します。

```hcl
{
  name      = "app"
  image     = "public.ecr.aws/nginx/nginx:1-alpine"
  essential = true

  portMappings = [{
    containerPort = 80
    protocol      = "tcp"
  }]

  logConfiguration = {
    logDriver = "awsfirelens"
    options = {
      Name            = "s3"
      bucket          = data.terraform_remote_state.infra.outputs.log_bucket_name
      region          = "ap-northeast-1"
      total_file_size = "1M"
      upload_timeout  = "1m"
      use_put_object  = "On" # 停止時のログ欠け対策
      s3_key_format   = "/app-logs/%Y/%m/%d/%H-%M-%S-$UUID.log"
    }
  }
}
```

隣にfluent-bitのコンテナを足してあげます。

```hcl
{
  name      = "log_router"
  image     = "public.ecr.aws/aws-observability/aws-for-fluent-bit:init-latest"
  essential = true

  firelensConfiguration = {
    type = "fluentbit"
    options = {
      enable-ecs-log-metadata = "true"
    }
  }

  # 設定ファイルのARN
  environment = [{
    name  = "aws_fluent_bit_init_s3_1"
    value = data.terraform_remote_state.infra.outputs.fluentbit_config_arn
  }]

  # fluent-bit自身のログ
  logConfiguration = {
    logDriver = "awslogs"
    options = {
      awslogs-group         = data.terraform_remote_state.infra.outputs.log_group_name
      awslogs-region        = "ap-northeast-1"
      awslogs-stream-prefix = "firelens"
    }
  }
}
```

fluent-bit自身のログはCloudWatchに残しておきます。
fluent-bitが落ちたときにはS3にも何も出ないので、調査手段を1つ残しています。

# ハマったポイント
構築していて詰まった部分をば

## FargateはS3から設定ファイルを直接読めない
fluent-bitの設定ファイルはS3のバケットに置いてあるので、最初は素直に`config-file-type = "s3"`と書きました。
これでapplyすると弾かれてしまいました。

```text
Error: creating ECS Task Definition (ecs-verify-app): ... ClientException:
Fargate launch type does not support FirelensConfiguration config file from 's3'
```

この書き方はEC2起動タイプ専用のようでした。
Fargateで同じことをやるには、起動時にS3から設定を取得する`init`タグのイメージに変えます。

ここも1回間違えたのですが、取得しているのはコンテナの中のプロセスなので、`s3:GetObject`は実行ロールではなくタスクロール側に要ります。
環境変数`aws_fluent_bit_init_s3_1`にファイルのARNを渡してあげると、initイメージがそれを`/init`配下に展開してくれます。

公式には「設定ファイルをS3に置くなら実行ロールに`s3:GetObject`が要る」と書いてあるので、逆に見えるかもしれません。

https://docs.aws.amazon.com/ja_jp/AmazonECS/latest/developerguide/task_execution_IAM_role.html

あちらは`config-file-type = "s3"`でECSのエージェントが取りに行く場合の話です。
initイメージは自分で取りに行くので、タスクロール側に要ります。
実行ロールからS3の権限を全部外しても動いたので、たぶんこの理解で合っています。

## initイメージに設定のパスを渡すとSIGSEGVで落ちる
initイメージに変えたあと、読ませたいファイルのパスも以下のように一応書いておいたのですが。。。

```hcl
firelensConfiguration = {
  type = "fluentbit"
  options = {
    config-file-type        = "file"
    config-file-value       = "/init/fluent-bit-init.conf"
    enable-ecs-log-metadata = "true"
  }
}
```

コンテナがexit code 139で即死。
ログはこれだけ。

```text
time="..." level=info msg="[FluentBit Init Process] Using /init/ directory"
Fluent Bit v1.9.10
[2026/09/12 03:52:13] [engine] caught signal (SIGSEGV)
```

initイメージは起動時に`/init/fluent-bit-init.conf`を自分で作って、その中でECSが生成した`/fluent-bit/etc/fluent-bit.conf`を`@INCLUDE`します。
そこへECS側からも`@INCLUDE /init/fluent-bit-init.conf`を足したので、2つのファイルが相互に参照して再帰しているようでした。止まってくれるのありがたい。
ですので**optionsに`enable-ecs-log-metadata`だけ書いて、設定ファイルの指定を一切しない**ことで解消。

## S3のライフサイクルが設定ファイルごと消しに来ていた
ログ用のバケットなので7日で失効するライフサイクルを付けていたのですが、対象の書き方が雑でした。

```hcl
rule {
  id     = "expire-7days"
  status = "Enabled"

  filter {
    prefix = "" # バケット全体
  }

  expiration {
    days = 7
  }
}
```

fluent-bitの設定ファイルは同じバケットの`config/fluent-bit.conf`に置いていたので、こちらにも削除予約が付いていました。
head-objectで現物を見ると`Expiration`が返ってきます。

```text
"Expiration": "expiry-date=\"Sun, 20 Sep 2026 00:00:00 GMT\", rule-id=\"expire-7days\""
```

厄介なのは、消えても稼働中のタスクは動き続けることです。
設定はすでに読み込まれているので、消えた時点では何も気づけません。
7日経ったあと、次にタスクが作り直されたところで初めて起動に失敗します。

Fargateのタスクはデプロイ以外にクラッシュやAWS側のメンテでも入れ替わるので、「何もしてないのに落ちた」になります。

ログのほうは`s3_key_format`で`app-logs/`配下に置くようにしていたので、ライフサイクルの対象もそこだけに限定して直しました。
設定ファイルを同じバケットに置くなら、消える対象から外れているかは見ておいたほうがよさそうです。

# fluent-bit.confの設定
S3に全部行くようにすると、今度はエラーに気づけなくなります。
なので全部S3に置いたうえで、エラーらしき行だけCloudWatchにも送ってあげます。

```yml
[FILTER]
    Name   grep
    Match  *
    Exclude log /health

[FILTER]
    Name         rewrite_tag
    Match        app-firelens-*
    Rule         $log (\[(error|crit|alert)\]|ERROR) error.$TAG true
    Emitter_Name emitter_error

[OUTPUT]
    Name              cloudwatch_logs
    Match             error.*
    region            ap-northeast-1
    log_group_name    /ecs/ecs-verify
    log_stream_prefix errors/
    auto_create_group false
```

上から順に、

1. `/health`を含む行を捨てる
1. エラーらしき行に`error.`始まりのタグを付けて複製する
1. `error.*`だけをCloudWatchに出す

をやっています。
S3出力はECSがタスク定義のoptionsから自動生成していて、そのMatchは`app-firelens-*`です。
`error.`始まりのタグはこれに前方一致しないので、S3に二重には行きません。

ここでCloudWatchに書く権限が実行ロールからタスクロールに移ります。
`awslogs`のときはECSエージェントが書いていたので権限は実行ロールの`AmazonECSTaskExecutionRolePolicy`に含まれていましたが、今はコンテナの中のfluent-bitが自分で`PutLogEvents`を叩きます。
ログの出し方を変えただけでIAMの持ち主が変わるので注意。

# 実測
nginxの初期状態にあるのは`index.html`だけなので、`/health`も`/boom`も存在しません。
どちらを叩いても404が返って、アクセスログとは別にerror_logが出ます。
この3つを叩いて、S3とCloudWatchの両方を見ていきます。

| 叩いたパス | S3 | CloudWatch |
| --- | --- | --- |
| `/`を3回 | アクセスログ3件 | 0件 |
| `/health`を3回 | 0件 | 0件 |
| `/boom`を2回 | アクセスログ2件とエラー2件 | エラー2件だけ |

`/health`のほうはerror_logの文字列にも`html/health`が含まれるので、アクセスログもerror_logもgrepフィルタで捨てられます。

```text
2026/09/12 11:32:37 [error] 31#31: *7 open() "/usr/share/nginx/html/boom" failed
(2: No such file or directory), client: 203.0.113.10, request: "GET /boom HTTP/1.1"
```

`/boom`のerror_logだけが入っていて、`/`のアクセスログはCloudWatch側に入っていません。

# Athena
S3だけだとただのテキストファイルが入っているだけなのでログ探索が地獄です。
tailもできず、`upload_timeout`の分だけ遅れて出てきます。
挙動の確認には十分ですが、障害対応でこれだけを使うのはしんどいです。
なのでAthenaを乗せておきます。

AthenaはS3の上にスキーマ定義だけ置いてSQLを投げる仕組みです。
fluent-bitの出力は1行1JSONなので、フィールドをそのまま列にするだけで済みます。
テーブル定義は省略します。

`date`はAthenaの予約語なので注意です。
fluent-bitが出すフィールド名がそのまま`date`なので、クエリでは`"date"`とダブルクォートで囲みます。

適当に叩いてみます。

```sql
-- 全件数
SELECT count(*) FROM ecs_verify.app_logs;

-- エラー行だけ取り出す
SELECT "date", log FROM ecs_verify.app_logs
WHERE log LIKE '%[error]%'
ORDER BY "date" DESC;

-- 日付で絞る（s3_key_formatの日付ディレクトリがそのままパーティションになる）
SELECT count(*) FROM ecs_verify.app_logs WHERE dt = '2026/09/12';

-- /healthが本当に捨てられているか
SELECT count(*) FROM ecs_verify.app_logs WHERE log LIKE '%health%';
```

結果はこうなりました。

| クエリ | 結果 | スキャン量 |
| --- | --- | --- |
| 全件数 | 55件 | 22KB |
| エラー行 | 2件 | 22KB |
| `dt = '2026/09/12'` | 55件 | 22KB |
| healthを含む行 | 0件 | 22KB |

Athenaは1クエリ最低10MBのスキャン課金なので、このデータ量だと実質ゼロです。

ちなみに検証まるごと1日で、Fargateが`$0.0099`、VPCが`$0.0033`、S3が`$0.00079`でした。
VPCの分はパブリックIPv4アドレスの料金です。

# まとめ
やっていること自体は`container_definitions`にサイドカーとしてコンテナを1つ足すだけで、アプリのコードは触りません。
そのかわり、ログが出ないときに見る場所がアプリの外に増えます。
`use_put_object`を切ったときにどのくらい欠けるのかは試していないので、そのうち見てみたいと思います。

# 参考
https://docs.aws.amazon.com/AmazonECS/latest/developerguide/using_firelens.html

https://docs.aws.amazon.com/AmazonECS/latest/developerguide/task_definition_parameters.html

https://github.com/aws/aws-for-fluent-bit/tree/mainline/use_cases/init-process-for-fluent-bit
