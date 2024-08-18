# ベースイメージとしてPHP-FPMを使用
FROM php:8.0-fpm

# New Relicの引数を定義
ARG NEW_RELIC_LICENSE_KEY
ARG NEW_RELIC_APP_NAME

# これらの引数を環境変数として設定
ENV NEW_RELIC_LICENSE_KEY=${NEW_RELIC_LICENSE_KEY}
ENV NEW_RELIC_APP_NAME=${NEW_RELIC_APP_NAME}

# New Relic PHPエージェントをインストール
FROM php:fpm

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