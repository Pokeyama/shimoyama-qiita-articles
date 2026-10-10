---
title: 【EC2】シンプルスケーリングとステップスケーリングの違い
tags:
  - 'AWS'
  - 'EC2'
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
細かくスケーリングを調整しないといけない案件に携わったとき、シンプルスケーリングとステップスケーリングについて学べたのでまとめます。
**EC2の話であり、ECSだとこの通りにはいかないので注意。**

動的スケーリングにはターゲット追跡もあって、AWSとしてはまずそっちを使ってほしいみたいですが今回は扱いません。

# 動的スケーリングポリシー
アラームで動くのはシンプルとステップの二つで、**基本的に**ステップはシンプルの上位互換です。
どちらもCloudWatchアラームをきっかけに台数を増減するので、増やすポリシーと減らすポリシーを別々に作ってアラームを紐づけておきます。
それぞれ違いを見ていきます。

## シンプルスケーリング
シンプルスケーリングは、アラームがALARMになったときに一定数だけインスタンスを増減するポリシーです。
例えばCPU使用率が70%を超えたら+1、60%を下回ったら-1のように2つ作ります。
条件を満たしたら必ず同じ数だけ増減するので、設計が簡単です。

![simple-scaling-policy.png](https://qiita-image-store.s3.ap-northeast-1.amazonaws.com/0/855584/80801105-5763-4266-8b51-cec3336be938.png)

### クールダウン
インスタンスの追加・削除が終わったあと、指定した秒数だけ次のスケーリングを待ちます。
デフォルトは300秒です。
一度スケールアウトした直後に、瞬間的に負荷が下がってスケールインが走るような揺り戻しを防げます。

### しきい値が単一
増減数が1つしかないうえに、スケーリング中やクールダウン中に来たアラームには反応しません。

:::note
シンプルスケーリングポリシーでは、進行中のスケーリングアクティビティまたはヘルスチェックの置き換えが完了し、**クールダウン期間が終わるのを待ってから**、追加のアラームに応答する必要があります。
:::

https://docs.aws.amazon.com/ja_jp/autoscaling/ec2/userguide/simple-scaling-policies.html

要するに急激に負荷が上がっても、+1してクールダウンを待って、また+1して、と1段ずつしか増えません。
十分な台数まで追従しきれないケースもあります。

## ステップスケーリング
ステップスケーリングは、アラームの閾値をどれだけ超えたかに応じて増減数を変える方式です。
例えばCPU使用率が60〜80%なら+1、80〜90%なら+2、90%以上なら+4のように設定できます。
負荷がわずかに超えた場合は少しだけ、急激に超えた場合は一気にスケールアウト、という使い分けができます。

![step-scaling-policy.png](https://qiita-image-store.s3.ap-northeast-1.amazonaws.com/0/855584/9f633883-0fb1-4961-89c4-828e21b4f8fb.png)

シンプルと違ってスケーリング中もアラームに反応してくれます。
ポリシーにクールダウンの設定はなく、代わりに新しく起動したインスタンスにウォームアップ時間があります。

## 基本的にステップを使用していれば問題ない
AWSのドキュメントでも、増減数が1種類しかなくてもステップを使うよう推奨されています。
シンプルが廃止されるわけではなさそうですが、クールダウンのページにいたってはベストプラクティスとしてシンプルスケーリング自体を使わないよう書いてあります。
なので**シンプルスケーリングの上位互換**と巷では言われています。

# シンプルスケーリングを採用するケース
**一度スケールしたら〇分間は絶対インスタンスを増減させたくない！！！**
こんなケースで採用することになると思います。
「いや、ステップはシンプルの上位互換だからステップでよくね？」という話なんですが、挙動に違いがあるのでその違いを書いていきます。

## ステップスケーリングではクールダウンを指定できない
ここが自分の中で混同していたのですが、前述した通りEC2のステップスケーリングではクールダウンを指定できません。
APIの`Cooldown`の説明にも以下のように書いてあります。

> Valid only if the policy type is SimpleScaling.

https://docs.aws.amazon.com/ja_jp/autoscaling/ec2/APIReference/API_PutScalingPolicy.html

ECSのサービスのスケーリング（Application Auto Scaling）はステップにも`Cooldown`があるので、ここがEC2と違うところです。

https://docs.aws.amazon.com/ja_jp/autoscaling/application/userguide/step-scaling-policy-overview.html

## クールダウンとウォームアップの違い
じゃあEC2のステップは何でスケーリングしすぎるのを防いでいるのかというと、ウォームアップです。
シンプルのクールダウンと比べながら、それぞれ細かく見ていきます。

### クールダウン時間（cooldown）
シンプルのところで書いた待ち時間ですが、ポイントは止まる範囲です。
止まるのはそのポリシーだけではなく、グループ内の**シンプルスケーリングのポリシー全部**です。

https://docs.aws.amazon.com/ja_jp/autoscaling/ec2/userguide/ec2-auto-scaling-scaling-cooldowns.html

ポリシーに書く`cooldown`はデフォルトの待ち時間を上書きするもので、止まる範囲が変わるわけではなさそう。（ドキュメントにはっきりとは書いていない）

また、何があろうとこの時間はスケールしない、というわけでもないです。
スケジュールされたアクションや、unhealthyになったインスタンスの置き換え、ステップやターゲット追跡のスケールアウトはクールダウンを待たずに動きます。
ステップやターゲット追跡でも、スケールインはクールダウン中だと遅れることがあるみたいです。
手動でのスケールもデフォルトでは待ちません。

terraformで書くとこのようになります。

```terraform:terraform
resource "aws_autoscaling_policy" "simple_scale_out" {
  name                   = "simple-scale-out"
  policy_type            = "SimpleScaling"
  autoscaling_group_name = aws_autoscaling_group.example.name
  adjustment_type        = "ChangeInCapacity"
  scaling_adjustment     = 1
  cooldown               = 300 # 省略時はASGのdefault_cooldown
}
```

### ウォームアップ時間（EstimatedInstanceWarmup）
こちらがややこしい上に直感的ではなく、ドキュメントの言葉をそのまま引用すると以下になります。

:::note
指定されたウォームアップ時間が終了するまで、インスタンスは Auto Scaling グループの集約された EC2 インスタンスメトリクスにカウントされません。
:::

https://docs.aws.amazon.com/ja_jp/autoscaling/ec2/userguide/as-scaling-simple-step.html

これだけ読んでもピンとこない。
もう少し理解しやすく要約すると、**起動直後のインスタンスは一時的にCPUが跳ねることがあるので、その値を平均に入れないための待ち時間です**。

メトリクスに入らないだけで、台数としては数えられます。
次のスケールアウトでは、ウォームアップ中のインスタンスもdesired capacityに入れて計算されます。
例えば10台のグループに、上の60〜80%なら+1、80〜90%なら+2、90%以上なら+4を設定していたとします。

1. CPU65%でアラーム → +1で11台
1. ウォームアップ中にCPU70%でまたアラーム → 同じステップなので11台のまま
1. ウォームアップ中にCPU85%でアラーム → +2の範囲だけど、すでに1台増えているので12台

同じステップの範囲でアラームが続いても台数はどんどん増えなくて、大きいステップに入ったときだけ差分が足されます。
文字だと意味不明なので図にすると以下のような感じです。

![warmup-timeline.png](https://qiita-image-store.s3.ap-northeast-1.amazonaws.com/0/855584/19542156-5a0a-4d39-917e-6d68f95431ea.png)

あとはウォームアップが終わるまで、スケーリングポリシーによるスケールインが全部止まります。
なのでスケールアウト直後の揺り戻しはステップでも防げます。
シンプルを選ぶ理由が残るのは、スケールアウト側も含めて止めたいときだと思います。

ウォームアップはポリシーの`estimated_instance_warmup`より、ASG側の`default_instance_warmup`で設定するのが推奨されています。
どちらも設定していないと、ASGのデフォルトクールダウンの値が使われます。

`step_adjustment`の上限・下限はアラームの閾値からの差分で書きます。（コンソールだと絶対値）

![step-adjustment.png](https://qiita-image-store.s3.ap-northeast-1.amazonaws.com/0/855584/2462d351-a3f9-4630-b298-2a8909d3cd2c.png)

terraformで書くと以下のような感じです。

```terraform:terraform
resource "aws_autoscaling_group" "example" {
  # 省略
  default_instance_warmup = 300
}

# CloudWatchアラーム
resource "aws_cloudwatch_metric_alarm" "cpu_high" {
  alarm_name          = "cpu-high-step"
  comparison_operator = "GreaterThanOrEqualToThreshold"
  evaluation_periods  = 2
  metric_name         = "CPUUtilization"
  namespace           = "AWS/EC2"
  statistic           = "Average"
  period              = 60
  threshold           = 60 # CPU60%以上でALARM

  dimensions = {
    AutoScalingGroupName = aws_autoscaling_group.example.name
  }

  alarm_actions = [aws_autoscaling_policy.step_scale_out.arn]
}

resource "aws_autoscaling_policy" "step_scale_out" {
  name                    = "step-scale-out"
  policy_type             = "StepScaling"
  autoscaling_group_name  = aws_autoscaling_group.example.name
  adjustment_type         = "ChangeInCapacity"
  metric_aggregation_type = "Average" # 省略してもAverage

  # 60〜80%
  step_adjustment {
    metric_interval_lower_bound = 0
    metric_interval_upper_bound = 20
    scaling_adjustment          = 1
  }
  # 80〜90%
  step_adjustment {
    metric_interval_lower_bound = 20
    metric_interval_upper_bound = 30
    scaling_adjustment          = 2
  }
  # 90%以上
  step_adjustment {
    metric_interval_lower_bound = 30
    scaling_adjustment          = 4
  }
}
```

ステップの範囲に重複や隙間があってはいけなくて、上限なしにできるのも1つだけです。
最後のステップだけ`metric_interval_upper_bound`を書かないようにしておきましょう。

# まとめ
- シンプルスケーリング →　`cooldown`でシンプルスケーリングのポリシー全体の再発動を抑制
- ステップスケーリング →　ウォームアップで新規インスタンスのメトリクス反映を待つ

ステップスケーリングはたしかに基本的にシンプルスケーリングの上位互換ですが、細かいところを見ていくと動作が違っていて頭が痛くなりました。
スケールインのとき急激に減るのが不安など、アウト/インで片方だけシンプルスケーリングを採用することもありかなと思います。
