---
title: 【Nginx+PHP-FPM】S3ストリームレスポンス中断問題の原因と対策
tags:
  - 'PHP'
  - 'Laravel'
  - 'Nginx'
  - 'S3'
private: false
updated_at: ''
id: null
organization_url_name: null
slide: false
ignorePublish: false
---
# はじめに
Nginx+PHP-FPM構成のAPIサーバーでS3から画像をストリーミングレスポンスするAPIを作成しました。
しかし画像が途中までしか送信されない問題で1日溶かしたのでまとめます。

# 環境
構成: ELB⇔EC2⇔S3
サーバー: EC2 (AmazonLinux)
PHP: 8.3
フレームワーク: Laravel11

# 現象
以下のように画像が途中までしか読み込めない。

<img src="https://qiita-image-store.s3.ap-northeast-1.amazonaws.com/0/855584/fc341833-9d77-4ce1-90a1-20239663f909.jpeg" width=50%>



・おおよそ200KB以上の画像で、受信が途中（約16.4KB）で中断されるときがある。
・開発者ツールで確認するとどの画像も16.4KBを受け取ったところでレスポンスが中断されている。
・ Content-Lengthは正常に受信できているが、受信したサイズと一致していないため、`ERR_HTTP2_PROTOCOL_ERROR`というエラーがフロント側で発生している。

https://kinsta.com/jp/knowledgebase/err_http2_protocol_error/

・ステータスコードは200で正常終了している。

# 処理
S3からストリームでダウンロードした`$stream`をそのままレスポンスしています。以下は実装例です。
```php
return response()->stream(function () use ($stream) {
    if (is_resource($stream)) {
        fpassthru($stream);
        flush();
    } else {
        echo $stream;
    }
}, $filename, $headers);
```

# 解決方法
NginxかPHP側にヘッダーを追加
```php:PHPで解消する場合
header('X-Accel-Buffering: no');
```

```nginx:Nginxで解消する場合
fastcgi_buffering off;
```
どちらかを設定することで、Nginxがバッファに途中までしかデータを貯めずに断続的に送信する問題を回避できます。
**バッファリング自体は通信を効率的に行うものなので、不必要にオフにはしないようにしましょう。**

以下興味ある人向け原因

# 原因
Nginxは、PHP-FPMからのレスポンスを一旦固定サイズのバッファに貯める仕組みになっています。

・EC2＋S3環境: S3からのストリームはネットワーク経由で受信するため、レスポンスが断続的になり、Nginxのバッファが固定サイズ（約16.4KB）までしか溜まらず、以降のデータが送信**されないことがある**。

:::note
FastCGIサーバーからのレスポンスを受信しているときの最大バッファサイズ。デフォルトでは 8k or 16k
:::

https://www.khstasaba.com/?p=800#:~:text=fastcgi_busy_buffers_size

・ローカルDocker環境: 後述で構築した環境では、ローカルのファイルは高速かつ安定に読み込めるため、Nginxのバッファが十分にデータを受け取れる。結果、問題が発生しにくい。

# 再現
以下は、Docker上でNginx+PHP-FPM(Laravel)環境を構築した例です。
なお、今回はS3の代わりにリモート画像URLからfopenでストリームダウンロードしてレスポンスしています。

環境: Mac M3 Sequoia

```yml:docker-compose.yml
services:
  php:
    build:
      context: .
      dockerfile: Dockerfile
    volumes:
      - ./laravel:/var/www/html
    environment:
      - APP_ENV=local
    expose:
      - "9000"

  nginx:
    image: nginx:stable-alpine
    ports:
      - "8080:80"
    volumes:
      - ./laravel:/var/www/html:ro
      - ./default.conf:/etc/nginx/conf.d/default.conf:ro
```

```Dockerfile:Dockerfile
FROM php:8.3-fpm

# 必要なパッケージのインストール（unzip, git, libzip-dev など）
RUN apt-get update && apt-get install -y \
    unzip \
    git \
    libzip-dev \
 && docker-php-ext-install zip pdo pdo_mysql

# Composer のインストール
RUN curl -sS https://getcomposer.org/installer | php -- --install-dir=/usr/local/bin --filename=composer

WORKDIR /var/www/html

# エントリポイントスクリプトをコピー
COPY entrypoint.sh /usr/local/bin/entrypoint.sh
RUN chmod +x /usr/local/bin/entrypoint.sh

# 独自の API コントローラーをコピー
# このファイルは後述の ImageStreamController.php です
COPY ImageStreamController.php /var/www/html/app/Http/Controllers/ImageStreamController.php

EXPOSE 9000

ENTRYPOINT ["entrypoint.sh"]
CMD ["php-fpm"]
```

```php
class ImageStreamController extends Controller
{
    public function streamImage(Request $request): StreamedResponse
    {
        $remoteImageUrl = 'http://example.com/example.jpg';
        $stream = fopen($remoteImageUrl, 'rb');
        if ($stream === false) {
            abort(500, "Error opening remote image");
        }
       
        return response()->stream(function () use ($stream) {
            // 8KBずつ読み込みながら出力
            while (!feof($stream)) {
                echo fread($stream, 8192);
                flush();
            }
            fclose($stream);
        }, 200, [
            'Content-Type' => 'image/jpeg'
        ]);
    }
}
```

# 結論
・**原因**: ネットワーク経由のS3ストリームの場合、Nginxの固定サイズバッファリングにより、途中でデータの送信が止まる可能性がある。

・**解決策**: X-Accel-Buffering: no ヘッダーをPHP側、または fastcgi_buffering off; をNginx側で設定する。

今回はサーバー側で解決したが、Nginxでバッファリング中は通信を中断しない設定などありそう。
また、ELB側の設定を見れる環境にないので、そっちにも原因ありそう。