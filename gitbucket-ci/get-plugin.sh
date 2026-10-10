#!/usr/bin/env bash
# gitbucket-ci-pluginのjarをGitHub Releasesから取ってきて ./plugins/ に置く。
# jar自体はリポジトリにコミットしない方針なので、起動前に一度これを実行する。
set -euo pipefail

PLUGIN_VERSION="${PLUGIN_VERSION:-1.11.0}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PLUGIN_DIR="${SCRIPT_DIR}/plugins"
JAR_NAME="gitbucket-ci-plugin-${PLUGIN_VERSION}.jar"
JAR_URL="https://github.com/takezoe/gitbucket-ci-plugin/releases/download/${PLUGIN_VERSION}/${JAR_NAME}"

mkdir -p "${PLUGIN_DIR}"

if [ -f "${PLUGIN_DIR}/${JAR_NAME}" ]; then
  echo "既に存在します: ${PLUGIN_DIR}/${JAR_NAME}"
  exit 0
fi

echo "ダウンロード: ${JAR_URL}"
curl -fsSL -o "${PLUGIN_DIR}/${JAR_NAME}" "${JAR_URL}"
echo "配置しました: ${PLUGIN_DIR}/${JAR_NAME}"
ls -l "${PLUGIN_DIR}"
