#!/bin/bash

# PHP-FPMをバックグラウンドで起動
php-fpm &

# Nginxをフォアグラウンドで起動
nginx -g "daemon off;"