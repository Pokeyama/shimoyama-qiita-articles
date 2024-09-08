---
title: 【DI】Service Locatorパターンで実装して失敗と感じた理由
tags:
  - ''
private: false
updated_at: ''
id: null
organization_url_name: null
slide: false
ignorePublish: false
---
# はじめに
大規模案件のサーバーサイドプログラムをService Locatorパターンで実装しました。
最初のリリースは乗り切ったのですが、終わってみて大失敗だったなと思ったので言語化して供養します。

# 対象読者
・オブジェクト指向はある程度理解してるけどコード設計をしたことがない人
・Service LocatorとDependency Injectionという設計パターンがある中で、前者がアンチパターンと呼ばれてる理由がピンときていない人

# 環境
言語 C# .NET8
DB Mysql Spanner

# Service Locatorとは


# コード的な意味で
## テスト大変すぎ

## 途中参画厳しすぎ

## 