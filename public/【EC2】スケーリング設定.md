---
title: 【EC2】シンプルスケーリングとステップスケーリングの違い
tags:
  - 'AWS'
  - 'EC2'
private: false
updated_at: ''
id: null
organization_url_name: null
slide: false
ignorePublish: false
---
# はじめに
細かくスケーリングを調整しないといけなくなり、シンプルスケーリングとステップスケーリングについて学べたのでまとめます。
**EC2の話であり、ECSだとこの通りにはいかないようなので注意。**

# 動的スケーリングポリシー
シンプルとステップの二つがありますが、**基本的に**ステップはシンプルの上位互換です。
それぞれ違いを見ていきます。

## シンプルスケーリング
シンプルスケーリングは、閾値に到達したときに 一定数だけインスタンスを増減 する非常にシンプルなポリシーです。
特徴は以下で名前通りシンプルに増減を設定できます。

### 単純明快

例えば「CPU 使用率が 70% を超えたらインスタンス数を +1、60% を下回ったら -1」のように設定します。

条件を満たしたら必ず同じ数（＝スケーリング・アクション）だけインスタンス数を変更するため、設計が簡単です。

// スケーリング設定のスクショ

### クールダウン期間（Cooldown）

インスタンス追加/削除後、指定した秒数（デフォルトで 300 秒）だけ再スケールを抑制します。

これにより、「一度スケールアウトした後に瞬間的な負荷減で直後にスケールインが走る」といったスパイクの揺り戻しを回避できます。

### しきい値が単一

CPU、メモリ、ネットワークなどさまざまな CloudWatch Alarm を組み合わせることはできますが、単純に「このアラームが OK→ALARM または ALARM→OK になったら同じ数だけ操作」という構造です。

そのため、急激な負荷上昇時には複数回トリガーが発生し、クールダウン期間に依存して十分な台数まで追従しきれないケースもあります。

## ステップスケーリング
ステップスケーリングは、**複数のしきい値**に応じて増減数を動的に割り当てる方式です。
負荷の度合いに応じて「+1」「+2」「+3」…と段階的に細かく増やすことができ、より柔軟なスケーリング制御が可能です。

以下のように細かい設定ができるので**シンプルスケーリングの上位互換**と巷では言われています。

### 段階的な閾値設定

例えば「CPU 使用率が 60〜80%：+1、80〜90%：+2、90%以上：+4」のように設定できる。

負荷がわずかに超えた場合は小規模なスケールアウト、急激に超えた場合は一気に大規模にスケールアウト という使い分けができる。

// ステップスケーリングのスクショ

### ウォームアップ時間（Warmup）
新規インスタンスが`InService`になってから CloudWatchメトリクスに反映されるまでの猶予時間を設定します。

# シンプルスケーリングを採用するケース
**一度スケールしたら〇分間は絶対インスタンスの増減させたくない！！！**
こんなケースで採用することになると思います。
「いや、ステップはシンプルの上位互換だからステップでよくね？」という話なんですが、挙動に違いがあるのでその違いを書いていきます。

## ステップスケーリングでは（EC2 ASGでは）クールダウンを明示的に指定できない
ここが自分の中で混在していたのですが、ステップスケーリングではクールダウン時間を明示的にできません。（似たようなことができないわけではない）

https://docs.aws.amazon.com/ja_jp/autoscaling/ec2/APIReference/API_PutScalingPolicy.html?utm_source=chatgpt.com#:~:text=Valid%20only%20if%20the%20policy%20type%20is%20SimpleScaling.%20For%20more%20information%2C%20see%20Scaling%20cooldowns%20for%20Amazon%20EC2%20Auto%20Scaling%20in%20the%20Amazon%20EC2%20Auto%20Scaling%20User%20Guide

### クールダウン時間(cooldown)
クールダウン時間はそのままの意味で、スケールアウト／スケールインを行った後、同じポリシーが再度実行されないように待機する時間です。

https://docs.aws.amazon.com/ja_jp/autoscaling/ec2/APIReference/API_ScalingPolicy.html?utm_source=chatgpt.com#:~:text=Required%3A%20No-,Cooldown,-The%20duration%20of

terraformで書くとこのようになります。

```terraform:terraform
resource "aws_autoscaling_policy" "simple_scale_out" {
  policy_type        = "SimpleScaling"
  autoscaling_group_name = aws_autoscaling_group.example.name
  adjustment_type    = "ChangeInCapacity"
  scaling_adjustment = 1
  cooldown           = 300   # この秒数だけ、同一ポリシーの再発動を抑制
}
```

ポイントとしては**クールダウン中は該当ポリシーのアクションが一切キャンセルされる（ほかのポリシーは影響なし**）ことです。
何があろうとこの時間はスケールしません。

### ウォームアップ時間(EstimatedInstanceWarmup)
こちらがややこしい上に直感的ではなく、ドキュメントの言葉をそのまま引用すると「**新しく起動したインスタンスが CloudWatch メトリクスに反映されるまでの猶予時間**」を定義する項目となります。

https://docs.aws.amazon.com/ja_jp/autoscaling/ec2/userguide/ec2-auto-scaling-default-instance-warmup.html?utm_source=chatgpt.com


たとえば以下の条件で設定するとします。

:::note warn
ステップスケーリング
CPU 使用率が一定以上: ＋2台
ウォームアップ: 300 秒
:::

実際の動作は以下となります。

:::note 
・起動したインスタンスが「InService」になってから 300 秒間は、当該インスタンスの CPU やネットワーク使用率が CloudWatch の平均値に加味されない

・その間にメトリクスを参照して新たなスケーリング判定が行われても、「まだウォームアップ中のインスタンス分が反映されていない」とみなして正しく判断し、過剰なスケールイン／スケールアウトを抑制できる 
:::

ややこしいことが書いてありますが、要するに
**スケール後、実際にインスタンスが稼働状態になってもウォームアップ時間分は実際のスケール台数としてカウントされない≒メトリクスに反映されない**（その間閾値アラートが継続していたらさらにスケールされる）ということになります。
ですので、急激な負荷にもある程度柔軟性を持ってスケールすることができます。

terraformで書くと以下のような感じです。
```terraform:terraform
# CloudWatchアラート設定
resource "aws_cloudwatch_metric_alarm" "cpu_high" {
  alarm_name          = "cpu_high_step"
  comparison_operator = "GreaterThanOrEqualToThreshold"
  evaluation_periods  = 2
  metric_name         = "CPUUtilization"
  namespace           = "AWS/EC2"
  statistic           = "Average"
  period              = 60
  threshold           = 60.0    # CPU ≥ 60% で Alarm

  dimensions = {
    AutoScalingGroupName = aws_autoscaling_group.example.name
  }

  alarm_actions = [
    aws_autoscaling_policy.step_scale.arn
  ]
}

resource "aws_autoscaling_policy" "step_scale" {
  policy_type              = "StepScaling"
  autoscaling_group_name   = aws_autoscaling_group.example.name
  estimated_instance_warmup = 300  # この秒数だけ“起動直後のインスタンス”のメトリクスを待機
  metric_aggregation_type  = "Average" # ウォームアップ中のインスタンスを除いたメトリクスを、Average で評価する
  step_adjustment {
    metric_interval_lower_bound = 30.0 # アラームの閾値差分が30を上回った場合1台増加
    scaling_adjustment          = 1
  }
  step_adjustment {
    metric_interval_lower_bound = 40.0
    scaling_adjustment          = 2
  }
}
```

# まとめ
- シンプルスケーリング →　`cooldown` で「ポリシー単体の再発動を抑制」

- ステップスケーリング →　`estimated_instance_warmup` で「新規インスタンスのメトリクス反映を待つ」

ステップスケーリングはたしかに**基本的に**シンプルスケーリングの上位互換ですが、細かいところを見ていくと動作が違っていて悩ませてくれました。
スケールインのとき急激に減るのが不安など、アウト/インで片方だけシンプルスケーリングを採用することもありかなと思います。
自分で好き勝手検証できる環境plz.
