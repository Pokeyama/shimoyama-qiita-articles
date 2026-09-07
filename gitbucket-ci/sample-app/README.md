# sample-app

[![build](http://localhost:8080/root/sample-app/build/main/badge.svg)](http://localhost:8080/root/sample-app/build)

gitbucket-ci-plugin の動作確認用の .NET 8 サンプル。

- `Calc` … 足し算・掛け算・割り算だけの最小クラス
- `Calc.Tests` … xUnit のテスト5件
- `ci.sh` … restore → build → test を順に流すビルドスクリプト

Settings → Build で「Enable build」を有効にし、Build script に `bash ci.sh` を入れておくと、
main への push ごとにこのスクリプトが走る。
