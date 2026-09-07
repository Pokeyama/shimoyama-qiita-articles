#!/usr/bin/env bash
# gitbucket-ci-pluginのビルドコマンド欄に貼る想定のスクリプト。
# テストが1件でも落ちれば非0で終了し、GitBucket側のビルドがFAILUREになる。
set -euo pipefail

# ci-pluginはビルドディレクトリ直下でスクリプトを実行するが、
# 手元からも同じスクリプトを叩けるようにスクリプト自身の位置へ移動する
cd "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

echo "==== 環境 ===="
echo "CI=${CI:-}"
echo "CI_BUILD_NUMBER=${CI_BUILD_NUMBER:-}"
echo "CI_BUILD_BRANCH=${CI_BUILD_BRANCH:-}"
echo "CI_COMMIT_ID=${CI_COMMIT_ID:-}"
echo "CI_REPO_SLUG=${CI_REPO_SLUG:-}"
dotnet --version

echo "==== restore ===="
dotnet restore SampleApp.sln

echo "==== build ===="
dotnet build SampleApp.sln --no-restore -c Release

echo "==== test ===="
dotnet test SampleApp.sln --no-build -c Release --logger "console;verbosity=normal"

echo "==== 成功 ===="
