---
title: GitBucketでGitHub Actionsっぽいことをしたい
tags:
  - GitBucket
  - CI
private: false
updated_at: ''
id: null
organization_url_name: advancednet-inc
slide: false
ignorePublish: false
---
# はじめに
GitBucketにはGitHub Actionsのような標準のCIがありません。
GitHubに慣れているとpushしたらテストくらい回ってほしくなるので、どこまでできるのか試してみました。

Jenkinsを別に立てて連携するとか、Webhookを受けて自前でactを動かすとかも考えたんですが、一番手軽そうなgitbucket-ci-pluginでやります。
Dockerで立てて、pushしたらdotnet testが回ってバッジとチェックが付くところまでを書きます。

# 環境
macOS
GitBucket 4.47.0
gitbucket-ci-plugin 1.11.0
**Dockerが入っていること**

# gitbucket-ci-plugin
takezoeさんが作っているプラグインです。

https://github.com/takezoe/gitbucket-ci-plugin

最新は1.11.0で、READMEに書いてある対応バージョンはGitBucket 4.35.x以上。
`GITBUCKET_HOME/plugins/`にjarを置いて起動すると読み込まれます。

# Dockerfile
最初の詰まりどころ。
`gitbucket/gitbucket:4.47.0`を指定したらnot foundでした。
Docker Hubのタグを見ると2022年12月の4.38.4が最後になっています。

GitHubにあるgitbucket-dockerのDockerfileのほうは4.47.0を指しているので、イメージのpushだけ止まっているっぽいです。
なので公式Dockerfileの中身を自前のDockerfileに写します。（.NETを動かすのでそれも入れておく）

```dockerfile:Dockerfile
FROM eclipse-temurin:17

ARG GITBUCKET_VERSION=4.47.0
ARG DOTNET_CHANNEL=8.0

# libicuがないとdotnetが落ちる
RUN apt-get update \
 && apt-get install -y --no-install-recommends \
      ca-certificates curl git unzip libicu-dev libssl-dev zlib1g \
 && rm -rf /var/lib/apt/lists/*

ADD https://github.com/gitbucket/gitbucket/releases/download/${GITBUCKET_VERSION}/gitbucket.war /opt/gitbucket.war

# dotnet-install.shがarm64/amd64を判別してくれる
ENV DOTNET_ROOT=/usr/share/dotnet
RUN curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh \
 && chmod +x /tmp/dotnet-install.sh \
 && /tmp/dotnet-install.sh --channel ${DOTNET_CHANNEL} --install-dir ${DOTNET_ROOT} \
 && ln -s ${DOTNET_ROOT}/dotnet /usr/local/bin/dotnet \
 && rm /tmp/dotnet-install.sh

ENV PATH="${PATH}:/usr/share/dotnet"

# ci-pluginがビルドごとにHOMEを差し替えるので、キャッシュはHOMEの外に置く
ENV NUGET_PACKAGES=/opt/nuget/packages
ENV DOTNET_CLI_HOME=/opt/dotnet-home
RUN mkdir -p /opt/nuget/packages /opt/dotnet-home

# ここから下は公式gitbucket-dockerと同じ
RUN ln -s /gitbucket /root/.gitbucket
VOLUME /gitbucket
EXPOSE 8080 29418
CMD ["sh", "-c", "java -jar /opt/gitbucket.war"]
```

`libicu-dev`を入れ忘れるとdotnetがグローバリゼーション関連で落ちるので注意です。

# キャッシュを使いたいとき
.NETのnugetみたいなやつはCIのたびに取ってくると時間がかかるのでキャッシュしたいですよね。
GitHub Actionsでは`actions/cache`でいい感じにやりますが、こちらは実質1コンテナなのでホスト側に置いておくだけでいいです。

ただしci-pluginはビルドごとにHOMEを使い捨てディレクトリに差し替えるので、デフォルトの`~/.nuget/packages`だと毎回消えます。
Dockerfileで`NUGET_PACKAGES`をHOMEの外に固定してあげて、その場所をcomposeのvolumeに載せるとキャッシュっぽい動きができまｓ。

```Dockerfile
# ci-pluginがビルドごとにHOMEを差し替えるので、キャッシュはHOMEの外に置く
ENV NUGET_PACKAGES=/opt/nuget/packages
ENV DOTNET_CLI_HOME=/opt/dotnet-home
RUN mkdir -p /opt/nuget/packages /opt/dotnet-home
```

```yml:docker-compose.yml
    volumes:
      - ./data:/gitbucket
      - ./plugins:/gitbucket/plugins
      - nuget-cache:/opt/nuget/packages
```

16秒かかっていたビルドが2秒で終わるようになりました。

# プラグインを読み込ませる
jarを取ってきて`./plugins`に置きます。

```sh
$ curl -fsSL -o plugins/gitbucket-ci-plugin-1.11.0.jar \
    https://github.com/takezoe/gitbucket-ci-plugin/releases/download/1.11.0/gitbucket-ci-plugin-1.11.0.jar
$ docker compose up -d --build
```

読み込まれたかはログでわかります。

```sh
$ docker compose logs | grep ci-plugin
INFO  g.core.plugin.PluginRegistry - Initialize gitbucket-ci-plugin-1.11.0.jar
```

`http://localhost:8080`にroot/rootでSign inして、アイコン→System administration→左メニューのPlugins。
CI Pluginが並んでいれば入っています。

![Plugins画面にCI Plugin(Id ci, Version 1.11.0)が表示されている](https://qiita-image-store.s3.ap-northeast-1.amazonaws.com/0/855584/483dd1ff-145d-4ab3-95ff-40c4c28074b9.png)

プラグイン自体は4.36.2でビルドされたものですが、4.47.0でも普通に読み込まれました。

# レポジトリを作ってビルドを有効にする
sample-appというレポジトリを作っておきました。

Settings→Buildタブに行くとci-pluginが足したビルド設定が出てきます。
Enable buildにチェックを入れて、Build scriptに`bash ci.sh`と書いてApply changes。

![SettingsのBuildタブでEnable buildにチェックし、Build scriptにbash ci.shを入れた画面](https://qiita-image-store.s3.ap-northeast-1.amazonaws.com/0/855584/dab4283d-999b-4d2a-8fcf-02bd19c6de5d.png)

Build typeは2つあって、Build scriptがこの画面に直接スクリプトを書く方式、Build fileがレポジトリ内のスクリプトファイルを指定する方式です。
Skip wordsの初期値は`[ci skip], [skip ci]`、Run wordsの初期値は`ok to test, test this please`。
Run wordsはPRのコメントに書くと再実行してくれるやつです。
E-mail notificationは失敗時のメール通知ですが、GitBucket側のSMTP設定が必要なので今回は入れていません。

# ci.sh
実際のCI用スクリプトはただの四則演算をするもので試してみました。
テストが1件でも落ちれば終了して、GitBucket側のビルドがFailureになります。

```sh:ci.sh
#!/usr/bin/env bash
set -euo pipefail

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
```

スクリプトの中で使える環境変数は以下です。

| 変数 | 中身 |
|---|---|
| `CI` | `true` |
| `HOME` | ビルドディレクトリ(ビルドごとに差し替わる) |
| `CI_BUILD_DIR` | ビルドディレクトリのルート |
| `CI_BUILD_NUMBER` | ビルド番号 |
| `CI_BUILD_BRANCH` | ブランチ名 |
| `CI_COMMIT_ID` | コミットのSHA |
| `CI_COMMIT_MESSAGE` | コミットメッセージ |
| `CI_REPO_SLUG` | `owner/repo` |
| `CI_PULL_REQUEST` | PR番号。PRでなければ`false` |
| `CI_PULL_REQUEST_SLUG` | PR元の`owner/repo`。PRでなければ空 |

準備ができたら手元からpushします。

```sh
$ git -c init.defaultBranch=main init
$ git add .
$ git commit -m "サンプルの電卓アプリとCIスクリプトを追加"
$ git remote add origin http://localhost:8080/git/root/sample-app.git
$ git push -u origin main
```

**デフォルトブランチはmainです。**
GitBucket 4.47の新規レポジトリはmainなので、masterで初期化してpushしても何も起きません。
最初これに気づかず、無反応なのでBuildの設定のほうを疑っていました。

# ビルド履歴
左メニューのBuildにビルド履歴が出ます。

![Build historyに#1 Successが5 secで表示されている](https://qiita-image-store.s3.ap-northeast-1.amazonaws.com/0/855584/e8420f45-5c39-4e4c-a36b-274254482704.png)

上に「You can add status badge by this URL」とバッジのURLが出ています。
Settingsのほうにあると思って最初探しました。

ビルド番号をクリックするとコンソール出力が見られます。

![ビルド#1のコンソール出力。テスト5件がPassedになっている](https://qiita-image-store.s3.ap-northeast-1.amazonaws.com/0/855584/519d686e-744f-40d7-8fb1-66edfea864da.png)

`git clone`と`git checkout`から始まって、ci.shのechoがそのまま流れてきます。
日本語のテスト名も文字化けせずそのまま出ています。

```text
  Passed Calc.Tests.CalculatorTests.Multiply_掛け算の結果が返る [2 ms]
  Passed Calc.Tests.CalculatorTests.Divide_ゼロ除算は例外になる [< 1 ms]
  Passed Calc.Tests.CalculatorTests.Add_足し算の結果が返る(a: 1, b: 2, expected: 3) [< 1 ms]

Test Run Successful.
Total tests: 5
     Passed: 5
```

# failureしてみる
`Calculator.Add`を`a - b`にしてpushしてみましょう。

```c#:Calc/Calculator.cs
public static int Add(int a, int b) => a - b;
```

![Build historyに#2 Failureが3 secで表示されている](https://qiita-image-store.s3.ap-northeast-1.amazonaws.com/0/855584/f82c21ca-a9fa-497f-a7d8-d178f1fa6c22.png)

ビルド#2がFailureになりました。
上のバッジ表示もfailureに変わっています。

![ビルド#2のコンソール出力。Test Run FailedとEXIT CODE 1で終わっている](https://qiita-image-store.s3.ap-northeast-1.amazonaws.com/0/855584/a9c75470-f75c-435d-9af8-595da7f90a82.png)

```text
  Failed Calc.Tests.CalculatorTests.Add_足し算の結果が返る(a: 1, b: 2, expected: 3) [< 1 ms]
  Error Message:
   Assert.Equal() Failure: Values differ
Expected: 3
Actual:   -1

Test Run Failed.
Total tests: 5
     Passed: 3
     Failed: 2
EXIT CODE: 1
```

最後の`EXIT CODE: 1`はci-pluginが出しています。
GitHub Actionsの`run`は`bash -e`で動くので途中で落ちれば止まりますが、ci-pluginは`#!/bin/sh`で素のまま実行するだけです。
`set -euo pipefail`を書いておけば、途中のコマンドが落ちた時点で止まって、そのコマンドの終了コードがそのままビルドの結果になります。

# バッジ
コミット一覧を見ると、GitHubのcommit statusと同じ位置にチェックが付いています。

![コミット一覧に1 success checksと1 failure checksが表示されている](https://qiita-image-store.s3.ap-northeast-1.amazonaws.com/0/855584/9c926884-6c47-4251-ad45-72d95bf99ef3.png)

READMEにバッジも貼っておきます。

```markdown:README.md
[![build](http://localhost:8080/root/sample-app/build/main/badge.svg)](http://localhost:8080/root/sample-app/build)
```

![レポジトリトップにbuild successのバッジが表示されている](https://qiita-image-store.s3.ap-northeast-1.amazonaws.com/0/855584/1dff818b-7a2d-4407-8a87-242b5563a4f6.png)

バッジがあると一気にそれっぽくなりますね。

# PR
PRも見ておきましょう。

![PR画面にAll is well 1 success checksとShow all checksが表示されている](https://qiita-image-store.s3.ap-northeast-1.amazonaws.com/0/855584/d920716f-af45-43ea-a08a-d59d2df895df.png)

「All is well」とShow all checksが出ます。
普段の使い方としてはここまでできれば十分ですね。

ビルド履歴を見ると、同じコミット`87671a8`が2回ビルドされていました。

![Build historyで87671a8がfeature/subtractとPR #1で2回ビルドされている](https://qiita-image-store.s3.ap-northeast-1.amazonaws.com/0/855584/2fb8e640-f0c1-460f-ad0a-362933b46178.png)

ブランチへのpushで#6、PRを作ったときに#7。
2秒で終わるので気になりませんが、重いビルドだとそこそこ無駄になりそう。（片方を止める設定があるのかは調べていない）

# CircleCI互換APIは500になる
ci-pluginはCircleCI互換のAPIも持っているんですが、こちらは動きませんでした。

```text
java.lang.NoSuchMethodError: 'void org.json4s.CustomSerializer.<init>(scala.Function1, scala.reflect.Manifest)'
	at io.github.gitbucket.ci.api.JsonFormat$.<clinit>(JsonFormat.scala:17)
```

プラグインが古いjson4sに対してコンパイルされているためで、GitBucket側が進んで合わなくなったのだと思います。

同じスタックトレースのissueが2023年に立っていて、今も開いたままでした。

https://github.com/takezoe/gitbucket-ci-plugin/issues/91

# 公開環境で使ってはいけない
ここまで動かしておいてなんですが、READMEのCautionsに書いてあります。

> Note that you must not use this plug-in in public environment because it allows executing any commands on a GitBucket instance. It will be **a serious security hole**.

GitBucketのインスタンス上で任意のコマンドが実行できてしまうので、公開環境で使ってはいけないみたいです。
作者様のブログにも日本語で同じことが書いてありました。

> このプラグインはリポジトリのオーナーにGitBucketが動作しているサーバ上で任意のコマンド実行を許可することになりますので、公開環境で運用しているGitBucketでは使用しないでください。

脆弱性ではなく、そういう作りだということですね。
どこまでいじれてしまうのかを一時的に以下へ差し替えて動かしてみました。

```sh
id
echo "HOME=$HOME"
pwd
ls /gitbucket
ls -l /gitbucket/data.mv.db
```

出力はこんな感じです。

```text
uid=0(root) gid=0(root) groups=0(root)
HOME=/root/.gitbucket/repositories/root/sample-app/build/8
/gitbucket/repositories/root/sample-app/build/8/workspace
activity.log
data.mv.db
database.conf
gist
plugins
repositories
tmp
-rw-r--r-- 1 root root 221184 Sep  5  2026 /gitbucket/data.mv.db
```

がっつりrootでした。
作業ディレクトリがGitBucketのデータディレクトリの中なので、他のレポジトリの実体も`database.conf`のパスワードも丸見えです。

社内の閉じたGitBucketなら問題にはならないはずですが、昨今のAI時代このへんで迂回して勝手に何かやられそうではあります。

# まとめ
ラップトップでやっているので、マシンスペックを気にしてたのですが割と動いてくれました。
10人規模くらいの社内サーバーに置く分には問題なさそうです。
rootを持ってしまうのは非常にまずいと思うので、本当に使うなら社内でも隔離した部分で使うのがまるそうではあります。

# 参考
https://github.com/gitbucket/gitbucket-docker

https://hub.docker.com/r/gitbucket/gitbucket

https://takezoe.hatenablog.com/entry/2017/09/30/230616

https://github.com/takezoe/gitbucket-ci-plugin/blob/master/src/main/scala/io/github/gitbucket/ci/manager/BuildJobThread.scala
