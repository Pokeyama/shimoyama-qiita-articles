#!/bin/sh
set -e

# /var/www/html が空の場合、Laravel プロジェクトを作成する
if [ -z "$(ls -A /var/www/html)" ]; then
  echo "Directory is empty. Creating Laravel project..."
  composer create-project --prefer-dist laravel/laravel . 
  php artisan key:generate
fi

exec "$@"
