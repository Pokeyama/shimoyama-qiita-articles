---
title: 【PHP】APIをNew Relicでエンドポイントごとにモニタリング with Docker
tags:
  - PHP
  - NewRelic
private: false
updated_at: '2024-08-19T03:04:54+09:00'
id: 2c099abea6b271366050
organization_url_name: null
slide: false
ignorePublish: false
---
# やりたいこと
PHPで書いたAPIのエンドポイント毎のパフォーマンスをNewRelicでざっくり見ていくまでを書きます。
APMを細かく分析していくところまでは書きません。
ホスト端末のCPU状況などは見ません。
無料枠を利用して、Dockerで完結させます。

# 環境
Mac M2

# NewRelicのアカウント作成
こちらから会員登録しておきます。
クレカ情報いらないので色々安心。

https://newrelic.com/jp/sign-up-japan

ログインしたらコンソールに飛ばされるので、LICENSE_KEYを控えておきます。
以下のようにデフォルトでもアカウントが作成されていると思うので、こちらのTypeがINGEST - LICENSEの三点リーダーをクリックしてCopy Keyで取得できます。
![スクリーンショット 2024-08-18 21.17.44.png](https://qiita-image-store.s3.ap-northeast-1.amazonaws.com/0/855584/c509142a-9d0c-9d49-b116-1540084aceec.png)

# Docker
```dockerfile
# ベースイメージとしてPHP-FPMを使用
FROM php:8.0-fpm

# New Relicの引数を定義
ARG NEW_RELIC_LICENSE_KEY
ARG NEW_RELIC_APP_NAME

# これらの引数を環境変数として設定
ENV NEW_RELIC_LICENSE_KEY=${NEW_RELIC_LICENSE_KEY}
ENV NEW_RELIC_APP_NAME=${NEW_RELIC_APP_NAME}

# Install the latest New Relic PHP Agent
RUN \
    cd /tmp \
    # Discover the latest released version:
    && export NEW_RELIC_AGENT_VERSION=$(curl -s https://download.newrelic.com/php_agent/release/ | grep -o '[1-9][0-9]\?\(\.[0-9]\+\)\{3\}' | head -n1) \
    # Discover libc provider
    && export NR_INSTALL_PLATFORM=$(ldd --version 2>&1 | grep -q musl && echo "linux-musl" || echo "linux") \
    # Download the discovered version:
    && curl -o newrelic-php-agent.tar.gz https://download.newrelic.com/php_agent/release/newrelic-php5-${NEW_RELIC_AGENT_VERSION}-${NR_INSTALL_PLATFORM}.tar.gz \
    # Install the downloaded agent:
    && tar xzf newrelic-php-agent.tar.gz \
    && NR_INSTALL_USE_CP_NOT_LN=1 NR_INSTALL_SILENT=0 ./*/newrelic-install install \
    # Configure the agent to use license key from NEW_RELIC_LICENSE_KEY env var:
    && sed -ie 's/[ ;]*newrelic.license[[:space:]]=.*/newrelic.license=${NEW_RELIC_LICENSE_KEY}/' $(php-config --ini-dir)/newrelic.ini \
    # Configure the agent to use app name from NEW_RELIC_APP_NAME env var:
    && sed -ie 's/[ ;]*newrelic.appname[[:space:]]=.*/newrelic.appname=${NEW_RELIC_APP_NAME}/' $(php-config --ini-dir)/newrelic.ini \
    # Cleanup temporary files:
    && rm newrelic-php-agent.tar.gz && rm -rf newrelic-php5-*-linux

# Nginxをインストール
RUN apt-get update && apt-get install -y nginx

# PHP-FPMのソケットパスを確認
RUN mkdir -p /run/php

# 作業ディレクトリを設定
WORKDIR /var/www/html

# 必要なファイルをコピー
COPY index.php /var/www/html/
COPY nginx.conf /etc/nginx/nginx.conf

# NginxとPHP-FPMを実行するためのスクリプトを作成
COPY start.sh /start.sh
RUN chmod +x /start.sh

# ポートを開放
EXPOSE 80

# コンテナ起動時にNginxとPHP-FPMを起動
CMD ["/start.sh"]
```

M系のCPUだとPHP8.0以上でしかNewRelicのエージェントが使えないみたいなので注意。

https://docs.newrelic.com/jp/docs/apm/agents/php-agent/installation/php-agent-installation-arm64/


エージェントの更新も頻繁にあるみたいなので、ドキュメントにある動的にバージョンを取ってくる記述にしたほうが無難だと思います。

https://docs.newrelic.com/jp/docs/apm/agents/php-agent/advanced-installation/docker-other-container-environments-install-php-agent/#install-same-container

# .env
先ほど記載した以下に対応する環境変数を.envで管理しておきます。
```dockerfile
# New Relicの引数を定義
ARG NEW_RELIC_LICENSE_KEY
ARG NEW_RELIC_APP_NAME

# これらの引数を環境変数として設定
ENV NEW_RELIC_LICENSE_KEY=${NEW_RELIC_LICENSE_KEY}
ENV NEW_RELIC_APP_NAME=${NEW_RELIC_APP_NAME}
```

NEW_RELIC_LICENSE_KEYがNewRelicコンソールで保存しておいたKey
NEW_RELIC_APP_NAMEが任意のアプリ名です。（なんでもいい）
```text:.env
NEW_RELIC_LICENSE_KEY={yourLicenseKey}
NEW_RELIC_APP_NAME={yourAppName}
```

# PHP
フレームワークは特に使わずに簡単なエンドポイントを用意します。

```php:index.php
<?php

header('Content-Type: application/json');

// New Relicでトランザクション名を設定
if (extension_loaded('newrelic')) {
    $request_uri = parse_url($_SERVER['REQUEST_URI'], PHP_URL_PATH);
    newrelic_name_transaction($request_uri);
}

// ルーティング処理
$request = parse_url($_SERVER['REQUEST_URI'], PHP_URL_PATH);

switch ($request) {
    case '/auth':
        authEndpoint();
        break;
    case '/login':
        loginEndpoint();
        break;
    case '/info':
        infoEndpoint();
        break;
    case '/start':
        startEndpoint();
        break;
    case '/result':
        resultEndpoint();
        break;
    default:
        http_response_code(404);
        echo json_encode(["message" => "Endpoint not found"]);
        break;
}

// 各エンドポイント
function authEndpoint() {
    echo json_encode(["message" => "Auth endpoint"]);
}

function loginEndpoint() {
    echo json_encode(["message" => "Login endpoint"]);
}

function infoEndpoint() {
    echo json_encode(["message" => "Info endpoint"]);
}

function startEndpoint() {
    echo json_encode(["message" => "Start endpoint"]);
}

function resultEndpoint() {
    echo json_encode(["message" => "Result endpoint"]);
}
```

味噌になるのがNewRelic用のトランザクション名設定です。
同じコンテナにNewRelicのエージェントをインストールしている場合newrelicモジュールが使えるようになっています。
こちらでトランザクション毎（エンドポイント毎）の名前を設定しておきます。
今回はURLをパースしてエンドポイント名を設定しておきます。

```php
// New Relicでトランザクション名を設定
if (extension_loaded('newrelic')) {
    $request_uri = parse_url($_SERVER['REQUEST_URI'], PHP_URL_PATH);
    newrelic_name_transaction($request_uri);
}
```

# その他（fpmとかnginxとか）　
その他のPHPを動かす設定は本筋とズレるので以下で確認してださい。

https://github.com/Pokeyama/shimoyama-qiita-articles/tree/main/php

# NewRelicで見てみる

dockerをビルドして.envを読み込ませながら実行します。

```sh
$ docker build -t my-php-nginx-app .
$ docker run --rm --env-file .env -p 8080:80 my-php-nginx-app
```

エンドポイントを叩いてみましょう。

```sh
$ curl http://localhost:8080/auth
curl http://localhost:8080/login
curl http://localhost:8080/info
curl http://localhost:8080/start
curl http://localhost:8080/result
{"message":"Auth endpoint"}{"message":"Login endpoint"}{"message":"Info endpoint"}{"message":"Start endpoint"}{"message":"Result endpoint"}
```

しばらく待つとNewRelicに反映されます。
コンソールのAPM&Servicesで先程任意に記述したAppNameが表示されています。
そこからTransactionページに遷移すると先ほどトランザクションを設定したエンドポイントの詳細が見られます。

![スクリーンショット 2024-08-18 22.20.41.png](https://qiita-image-store.s3.ap-northeast-1.amazonaws.com/0/855584/2d32806c-c168-8bc3-8bf8-0f2a4119f46c.png)

どれも同じ処理でわかりづらいのでわざとボトルネックを作ってみます。
/loginを1秒止めてみます。
```php:index.php
function loginEndpoint() {
    sleep(1);
    echo json_encode(["message" => "Login endpoint"]);
}
```

もう一度叩いたあと2分ほど待ってNewRelicを確認すると/loginだけ異常に時間がかかっていることがわかります。

![スクリーンショット 2024-08-18 22.45.02.png](https://qiita-image-store.s3.ap-northeast-1.amazonaws.com/0/855584/c03b8ca0-f3a9-726c-5789-e21e6a294f94.png)

# まとめ
ざっくりどのエンドポイントがどのくらい時間がかかっているかを確認できるところまで行いました。
NewRelicはまだまだいろいろできるのでそちらも今後書いていきたいと思います。

今回の検証用レポジトリ

https://github.com/Pokeyama/shimoyama-qiita-articles/tree/main/php
