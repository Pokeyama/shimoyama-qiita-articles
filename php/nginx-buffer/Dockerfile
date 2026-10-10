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
