# GitBucket + gitbucket-ci-plugin + .NET のローカル検証環境

GitBucket 4.47.0 に gitbucket-ci-plugin 1.11.0 を入れ、C#プロジェクトの `dotnet test` をCIで回すためのDocker環境です。

## 構成

| ファイル | 内容 |
| --- | --- |
| `Dockerfile` | eclipse-temurin:17 に gitbucket.war 4.47.0 と .NET SDK 8.0、git を入れる |
| `docker-compose.yml` | 8080/29418公開、`./data` と `./plugins` を永続化 |
| `get-plugin.sh` | ci-pluginのjarをGitHub Releasesから `./plugins/` へ取得 |
| `sample-app/` | クラスライブラリ `Calc` と xUnitテスト `Calc.Tests` |
| `sample-app/ci.sh` | CI設定画面に貼るビルドスクリプト |

Docker Hubの `gitbucket/gitbucket` は4.38.4(2022年)で更新が止まっているため、
公式リポジトリのDockerfileと同じ構成をこちらで組み直しています。

## 起動

```bash
./get-plugin.sh              # ci-pluginのjarを取得
docker compose up -d --build # 初回のビルドは2分ほど
```

http://localhost:8080 を開いて `root` / `root` でログインします。

プラグインが読み込まれたかはログで確認できます。

```bash
docker compose logs | grep ci-plugin
# Initialize gitbucket-ci-plugin-1.11.0.jar
```

## CIの設定

リポジトリの Settings → Build で以下を設定します。

- Enable build にチェック
- Build type: `script`
- Build script: `bash ci.sh`

デフォルトブランチ(GitBucket 4.47の新規リポジトリは `main`)にpushするとビルドが走ります。

## 既知の問題

- `./plugins` にはGitBucketが同梱プラグイン(gist/emoji/pages/notifications)も展開するため、
  起動後はci-plugin以外のjarも並びます。`.gitignore` で `plugins/*.jar` をまとめて除外しています。
- ci-plugin 1.11.0 の CircleCI互換API (`/api/circleci/v1.1/...`) は GitBucket 4.47.0 では
  `NoSuchMethodError: org.json4s.CustomSerializer.<init>` で500になります。
  プラグインが古いjson4sに対してコンパイルされているためで、Build画面自体は問題なく動きます。

## テストを手元で回す

ホストに.NET SDKが無くてもコンテナ内で実行できます。

```bash
docker run --rm -v "$PWD/sample-app:/work" -w /work gitbucket-ci-dotnet:4.47.0 ./ci.sh
```

## 停止と削除

```bash
docker compose down          # 停止(データは ./data に残る)
docker compose down -v       # NuGetキャッシュのボリュームも削除
rm -rf data                  # GitBucketのデータを完全に消す
```
