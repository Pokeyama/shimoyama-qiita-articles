# 記事のスクショ撮影用。max_size = 0 なのでEC2は起動しない
# 撮り終わったら terraform destroy で全部消す

terraform {
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = ">= 5.0"
    }
  }
}

variable "region" {
  default = "ap-northeast-1"
}

variable "account_id" {
  description = "実行先のAWSアカウントID。違うアカウントだとplanの時点で止まる"
  type        = string
}

provider "aws" {
  region              = var.region
  allowed_account_ids = [var.account_id]

  default_tags {
    tags = {
      Name    = "qiita-scaling-demo"
      Purpose = "qiita-screenshot" # 消し忘れ確認用
    }
  }
}

# 既存のネットワークに触らないよう専用のVPCを作る（VPC・サブネットは無料）
resource "aws_vpc" "demo" {
  cidr_block = "10.123.0.0/16"
}

resource "aws_subnet" "demo" {
  vpc_id     = aws_vpc.demo.id
  cidr_block = "10.123.1.0/24"
}

data "aws_ssm_parameter" "al2023" {
  name = "/aws/service/ami-amazon-linux-latest/al2023-ami-kernel-default-x86_64"
}

resource "aws_launch_template" "example" {
  name_prefix   = "qiita-scaling-demo-"
  image_id      = data.aws_ssm_parameter.al2023.value
  instance_type = "t3.micro"
}

resource "aws_autoscaling_group" "example" {
  name                = "qiita-scaling-demo"
  min_size            = 0
  max_size            = 0 # アラームが鳴っても起動させない
  desired_capacity    = 0
  vpc_zone_identifier = [aws_subnet.demo.id]

  launch_template {
    id      = aws_launch_template.example.id
    version = "$Latest"
  }

  default_instance_warmup = 300
}

# シンプルスケーリング（CPU70%超で+1、60%未満で-1）
resource "aws_cloudwatch_metric_alarm" "cpu_high_simple" {
  alarm_name          = "qiita-scaling-demo-cpu-high-simple"
  comparison_operator = "GreaterThanThreshold"
  evaluation_periods  = 2
  metric_name         = "CPUUtilization"
  namespace           = "AWS/EC2"
  statistic           = "Average"
  period              = 60
  threshold           = 70

  dimensions = {
    AutoScalingGroupName = aws_autoscaling_group.example.name
  }

  alarm_actions = [aws_autoscaling_policy.simple_scale_out.arn]
}

resource "aws_cloudwatch_metric_alarm" "cpu_low_simple" {
  alarm_name          = "qiita-scaling-demo-cpu-low-simple"
  comparison_operator = "LessThanThreshold"
  evaluation_periods  = 2
  metric_name         = "CPUUtilization"
  namespace           = "AWS/EC2"
  statistic           = "Average"
  period              = 60
  threshold           = 60

  dimensions = {
    AutoScalingGroupName = aws_autoscaling_group.example.name
  }

  alarm_actions = [aws_autoscaling_policy.simple_scale_in.arn]
}

resource "aws_autoscaling_policy" "simple_scale_out" {
  name                   = "simple-scale-out"
  policy_type            = "SimpleScaling"
  autoscaling_group_name = aws_autoscaling_group.example.name
  adjustment_type        = "ChangeInCapacity"
  scaling_adjustment     = 1
  cooldown               = 300
}

resource "aws_autoscaling_policy" "simple_scale_in" {
  name                   = "simple-scale-in"
  policy_type            = "SimpleScaling"
  autoscaling_group_name = aws_autoscaling_group.example.name
  adjustment_type        = "ChangeInCapacity"
  scaling_adjustment     = -1
  cooldown               = 300
}

# ステップスケーリング（記事のコードと同じ）
resource "aws_cloudwatch_metric_alarm" "cpu_high" {
  alarm_name          = "qiita-scaling-demo-cpu-high-step"
  comparison_operator = "GreaterThanOrEqualToThreshold"
  evaluation_periods  = 2
  metric_name         = "CPUUtilization"
  namespace           = "AWS/EC2"
  statistic           = "Average"
  period              = 60
  threshold           = 60

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
  metric_aggregation_type = "Average"

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
