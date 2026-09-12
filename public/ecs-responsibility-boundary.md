---
title: 【AWS】ECSだと開発会社とインフラ側で責務が混ざる話
tags:
  - AWS
  - ECS
  - Fargate
  - Terraform
private: false
updated_at: ''
id: null
organization_url_name: advancednet-inc
slide: false
ignorePublish: false
---

# はじめに
弊社はサーバー側の開発を担当していて、インフラは別のベンダー様が受け持つ体制で案件を進めることが多いです。
こういった開発体制のときEC2やk8sならマニフェストファイルなどがあるのでサーバーとインフラの責務がはっきりしています。
しかし、ECSを使うとサーバー側とインフラ側の線がどこなのかわからなくなります。
実際に構築して言語化してみてどこらへんが落としどころなのか検討してみました。

ECSの導入を検討している方や、すでに使っていて分担に悩んでいる方向けの内容になります。
構築手順そのものは書きません。

折衷案だけ読みたい方は[こちら](#折衷案)

# 検証した構成
Terraformのディレクトリを責務ごとに2つに分けて、それぞれ別々にapplyする形にしました。

```
.
├── infra/          インフラ側が持つ想定
│   ├── main.tf         VPC、ECSクラスタ、IAM、ロググループ、S3バケット
│   ├── outputs.tf      サーバー側に渡す値
│   └── fluent-bit.conf
└── app/            サーバー側が持つ想定
    └── main.tf         タスク定義、ECSサービス
```

`app/`は`terraform_remote_state`で`infra/`のoutputsだけを参照していて、インフラ側のリソースを直接指定している箇所はありません。
`outputs.tf`に並んだ値が、そのまま両社間の受け渡し口になります。

動かしたのはnginxを1つFargateで立ち上げるだけの構成です。

なおサーバー側がTerraformを持つ形にしたのは、今回そうしてみただけで決まりではありません。
インフラ側が全部持つ会社もあると思います。
このへんは最後の折衷案の節でまとめて書きます。

# タスク定義にサーバーとインフラの領域が同居する
ECSで責務が曖昧になる理由は、たぶんこれでほぼ全部です。

アプリを1つ動かすための成果物が、タスク定義とECSサービスというAWSのリソースそのものになります。
実際に登録されたタスク定義に、誰の関心事かを注記すると以下のような感じです。
`★`を付けた行が、決める人と書く人が分かれている箇所になります。

```jsonc
{
  "family": "ecs-verify-app",      // インフラ側: 命名規約
  "requiresCompatibilities": ["FARGATE"],  // インフラ側: 実行基盤の選択
  "networkMode": "awsvpc",         // インフラ側: FARGATEでは固定
  "cpu": "256",                    // ★ 決めるのはサーバー、払うのはインフラ
  "memory": "512",                 // ★ 同上
  "executionRoleArn": "...",       // インフラ側: IAM
  "taskRoleArn": "...",            // ★ 要る権限を知るのはサーバー、書くのはインフラ
  "containerDefinitions": [
    {
      "name": "app",
      "image": "public.ecr.aws/nginx/nginx:1-alpine",  // サーバー側
      "portMappings": [{ "containerPort": 80 }],       // サーバー側
      "logConfiguration": {      // ★ 書く場所はここ、送り先はインフラ側が決める
        "logDriver": "awsfirelens",
        "options": { "Name": "s3", "bucket": "ecs-verify-logs-..." }
      }
    },
    {
      "name": "log_router",      // ★ 器はここ、中身の設定はインフラ側
      "image": "public.ecr.aws/aws-observability/aws-for-fluent-bit:init-latest",
      "firelensConfiguration": { "type": "fluentbit" }
    }
  ]
}
```

EC2であればサーバー側が用意するのはビルドした成果物だけで、ここに並ぶ項目は1つも出てきません。
k8sであればマニフェストに書きますが、それはKubernetesのオブジェクトであってAWSリソースではないです。
**ECSだけが、サーバー側の成果物の中にインフラ側の関心事を混ぜ込んできます。**

以下、個別に見ていきます。

## ECSのIAM
ECSのタスクにはIAMロールを2つ書く場所があります。

| | 使うのは | 要る場面 |
| --- | --- | --- |
| `executionRoleArn` | ECS本体 | イメージのpull、ログドライバのCloudWatch書き込み |
| `taskRoleArn` | コンテナの中のプロセス | アプリがAWSのAPIを叩くとき |

https://docs.aws.amazon.com/ja_jp/AmazonECS/latest/developerguide/task_execution_IAM_role.html

https://docs.aws.amazon.com/ja_jp/AmazonECS/latest/developerguide/task-iam-roles.html

実行ロールのほうのページに「コンテナから直接アクセスすることはできません」と書いてあって、この2つが別物なのが分かります。

nginxを立ち上げるだけの最小構成では、前者しかありませんでした。
コンテナの中からAWSを触らない場合不要ということですね。

### taskRoleArn
例えばアプリがS3にファイルを置くようになったとき、と考えると分かりやすいです。
コンテナの中のプロセスが自分でAWSのAPIを叩くので、そこで初めて`taskRoleArn`が要ります。

今回はログの送り先をS3に変えたときに出てきました。
ログを運ぶコンテナがS3にPUTするので、その権限が要ります。

どのバケットにどの操作が要るかを知っているのはサーバー側で、IAMロールとポリシーを書くのはインフラ側です。

### サーバー側にも追記が必要
このときサーバー側のTerraformにも以下を書き足しました。

```hcl
task_role_arn = data.terraform_remote_state.infra.outputs.task_role_arn
```

同じものがインフラ側にはこう書いてあります。

```hcl
policy = jsonencode({
  Version = "2012-10-17"
  Statement = [
    {
      Effect   = "Allow"
      Action   = "s3:PutObject"
      Resource = "${aws_s3_bucket.logs.arn}/*"
    },
    # このあとGetObjectとCloudWatchに書く分が続きます
  ]
})
```

名前だけ書いて中身はインフラ側、というのはk8sのServiceAccountでも同じです。
ここ自体は普通のことだと思っています。

EC2だけは違っていて、決まった場所に成果物を置くだけなので、権限が増えてもサーバー側は何も変わりません。
インスタンスプロファイルはインスタンスに付くので、成果物の側に書く欄がそもそもないです。

## logConfigurationは書く場所と決める場所がずれる
ログの設定はタスク定義の中に書きます。
ECSをそのまま作るとこうなります。

```hcl
logConfiguration = {
  logDriver = "awslogs"
  options = {
    awslogs-group  = "/ecs/ecs-verify"
    awslogs-region = "ap-northeast-1"
  }
}
```

書く場所はタスク定義なのでサーバー側です。
ですがこのロググループを作るのも、保持期間を何日にするか決めるのもインフラ側です。

保持期間は障害調査ができるかどうかを左右する値ですが、サーバー側は決められません。
決められないのに記述だけサーバー側のファイルにあるという状況になります。

k8sならアプリはstdoutに出すだけで、集めるのはクラスタ側のDaemonSetです。
書く欄がサーバー側に無いぶん、ここはECSのほうが中途半端だと思っています。

## サイドカーの設定ファイルを触れない
ログをCloudWatchから逃がすため、FireLensのサイドカーを足しました。
タスクの中にfluent-bitのコンテナを1つ同居させて、nginxのログをそちらに横流しする形です。

<!-- TODO: FireLens記事のURLを貼る -->

コンテナの定義はタスク定義の中なのでサーバー側です。
ですが何を捨てるかを書いた設定ファイルは、インフラ側が持つS3のバケットに置きました。

```
[FILTER]
    Name   grep
    Match  *
    Exclude log /health
```

ヘルスチェックのログは捨てる、というだけの4行。
捨てていいと判断できるのはアプリの仕様を知っている側だけなのに、1文字変えるのにインフラ側のapplyが要ります。

イメージに焼けばサーバー側に持ってこられますが、そのためにECRと自前イメージの管理が増えます。
k8sならConfigMapを同じnamespaceに置けるので、ここまで固くはならないはずです。

## assignPublicIpがサーバー側の記述に混ざる
`assignPublicIp`はタスクにグローバルIPを付けるかどうかの設定です。
付ければタスクが直接インターネットに出られますし、付けなければNAT Gatewayなどの出口を別途用意することになります。

ECSサービスのネットワーク設定は以下のようになりました。

```hcl
network_configuration {
  subnets          = data.terraform_remote_state.infra.outputs.subnet_ids
  security_groups  = [data.terraform_remote_state.infra.outputs.security_group_id]
  assign_public_ip = true
}
```

3行のうち2行はインフラ側から来た値ですが、`assign_public_ip`だけはサーバー側の直書きです。
どこからどう繋がるかの設計なので、本来はインフラ側の話だと思います。

今回はNAT Gatewayを作らないコスト都合でこうしたんですが、その判断の記録がアプリのコードに紛れ込んでいます。
仮にインフラ側が「タスクはプライベートサブネットに置く」と方針を決めても、サーバー側のコードを直さないと守れません。

k8sだとPodをどのサブネットに置くかはクラスタ側の設定なので、アプリのマニフェストからは触れません。

# インフラ側から受け取る値が5個から8個に増えた
nginxを1つ動かすだけでも、サーバー側のコードはインフラ側の値だらけになりました。

```hcl
cluster            = data.terraform_remote_state.infra.outputs.cluster_name
subnets            = data.terraform_remote_state.infra.outputs.subnet_ids
security_groups    = [data.terraform_remote_state.infra.outputs.security_group_id]
execution_role_arn = data.terraform_remote_state.infra.outputs.execution_role_arn
log_group          = data.terraform_remote_state.infra.outputs.log_group_name
```

ログをS3に逃がしたところで、タスクロールのARN、バケット名、設定ファイルのARNの3つが増えました。
サイドカー1つで6割増。

同じことをk8sでやると、Deploymentはこれで足ります。

```yaml
spec:
  template:
    spec:
      containers:
        - name: app
          image: nginx:1-alpine
          ports:
            - containerPort: 80
```

サブネットIDもロールのARNも出てきません。
namespaceに置けばネットワークもログ収集もクラスタ側がやってくれます。
EC2にいたっては、成果物の側にこの手の値が出てくること自体がありません。

**増えたぶんだけ2社間のやり取りが増えます。**

あともう1つ、applyの順番が固定されました。
サーバー側はインフラ側のoutputsを参照しているので、インフラ側をapplyして`task_role_arn`が生えるまで、サーバー側は`terraform plan`すら通りません。
インフラ側のapplyが終わるまで、サーバー側は動作確認どころかplanもできない同期的な状態になってしまいました。

# 折衷案
分け方は3つあると思っています。

1. サーバー側がTerraformを持つ（今回やった形）
1. インフラ側が全部持つ
1. タスク定義をJSONにしてアプリのレポジトリへ置き、CIは2つのAPIだけを叩く

1はタスク定義もサービスもサーバー側の裁量で変えられますが、サーバー側にAWSのクレデンシャルとECS・IAMを触る権限が渡ります。
2は境界としては一番きれいで、サーバー側にAWS権限は一切渡りません。
代わりにイメージのタグを1つ上げるみたいな変更でも依頼票になるので、CI/CDが成立しません。

なので個人的には3を推しています。
インフラ側がTerraformでVPCからECSサービスの箱までを持ちます。
サーバー側が持つのは、タスク定義をそのまま書いたJSONファイル（ここでは`taskdef.json`）とCIの設定だけです。

CIがやるのはこれだけになります。

```sh
# タスク定義を登録して、新しいrevisionのARNを受け取る
TD=$(aws ecs register-task-definition \
  --cli-input-json file://taskdef.json \
  --query 'taskDefinition.taskDefinitionArn' --output text)

# そのrevisionでサービスを更新する
aws ecs update-service --cluster ecs-verify --service ecs-verify-app \
  --task-definition "$TD"
```

サーバー側に渡すIAMポリシーは以下のような形になります。

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Sid": "RegisterTaskDefinition",
      "Effect": "Allow",
      "Action": ["ecs:RegisterTaskDefinition", "ecs:DescribeTaskDefinition"],
      "Resource": "*"
    },
    {
      "Sid": "UpdateOnlyThisService",
      "Effect": "Allow",
      "Action": ["ecs:UpdateService", "ecs:DescribeServices"],
      "Resource": "arn:aws:ecs:ap-northeast-1:123456789012:service/ecs-verify/ecs-verify-app"
    },
    {
      "Sid": "PassOnlyEcsVerifyRoles",
      "Effect": "Allow",
      "Action": "iam:PassRole",
      "Resource": [
        "arn:aws:iam::123456789012:role/ecs-verify-execution-role",
        "arn:aws:iam::123456789012:role/ecs-verify-task-role"
      ],
      "Condition": {
        "StringEquals": { "iam:PassedToService": "ecs-tasks.amazonaws.com" }
      }
    }
  ]
}
```

`ecs:RegisterTaskDefinition`は`"Resource": "*"`にせざるを得ません。
このAPIはリソースレベルの権限指定に対応していないので、familyを絞れません。
サーバー側は好きな名前のタスク定義を登録できてしまうので、ここは絞れない前提で設計しておきます。

`iam:PassRole`のほうは絞らないと権限昇格になるので注意です。
`"Resource": "*"`のままだと、アカウント内の任意のIAMロールをタスクに渡して、そのロールの権限でコンテナを実行できます。
Administrator相当のロールを`taskRoleArn`に書いたタスク定義を登録して`update-service`すれば、コンテナの中からアカウント全体を触れてしまいます。
**ロールのARNに限定した上で、`iam:PassedToService`の条件も必ず付けましょう。**

やっていることは、k8sのマニフェストとRBACをECSの上で手作りしているだけです。

| k8s | 折衷案での対応物 |
| --- | --- |
| Deploymentのマニフェスト | `taskdef.json`（アプリのレポジトリ） |
| `kubectl apply` | `register-task-definition`と`update-service` |
| RBAC | IAMポリシー（Actionを2つに、ResourceをサービスARNに限定） |
| namespace | クラスタとサービスARN（強制力は弱い） |
| ResourceQuota | 対応物なし |

ResourceQuotaみたいに上限を縛るものだけは作れませんでした。
`cpu`も`memory`も`desiredCount`もそのまま請求額ですが、ECSにはこれを縛る仕組みがありません。
IAMの`Condition`でタスク定義のcpu値を制限することもできないです。

## この方式の微妙な部分
インフラ側の`aws_ecs_service`に`ignore_changes`が必須になります。

```hcl
lifecycle {
  ignore_changes = [task_definition, desired_count]
}
```

これを書かないと、インフラ側が別件で`terraform apply`しただけでCIがデプロイした最新revisionが巻き戻ります。（一番インフラ側が嫌なやつ）

ですが書いた時点で、Terraformのコードは現実を表さなくなります。
`terraform plan`が差分なしでも、実際に動いているタスク定義はコードと違いますよね。
「**Terraformを見れば今の構成が分かる**」という前提を捨てることになります。

（この前提は`ignore_changes`を入れる前から崩れかけていて、`aws_ecs_task_definition`はAWSが`environment = []`や`user = "0"`などのデフォルトを埋め戻すので、何も変えていなくても毎回planに差分が出ます）

ロールバックもrevision番号の運用になります。
どのrevisionが正常だったかを、Gitのタグとの対応表か何かで残しておかないといけません。

そして一番大事なところですが、この折衷案で解消するのは「サーバー側がAWSリソースを作る権限を持ってしまう」問題だけです。
`taskRoleArn`の中身もロググループの保持期間も依然インフラ側なので、関心事が2社にまたがるほうは何も解決しません。悲しみ。

# まとめ
ECSを選ぶと、アプリを1つ動かすという行為がそのままAWSのAPIを叩く行為になります。
サーバー側がAWSアカウントの中の人になることが、構造的に決まってしまうという理解でいます。

折衷案の3を推しはしますが、これはk8sなら最初から付いてくるものを手で組み立てているだけです。
「ECSはk8sより運用が楽」と判断するときに、この組み立ての分は計上しておいたほうがよさそう。
これでもk8sより使い方自体はシンプルだよなあとは思っています。k8sむずい。。。

# 参考
https://docs.aws.amazon.com/AmazonECS/latest/developerguide/task_definition_parameters.html

https://qiita.com/simoyama2323/items/3acdf6d66d04086feeae
