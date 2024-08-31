---
title: qiita-cliからの投稿時にXに自動でポストできるようにする
tags:
  - Node.js
  - TypeScript
  - GitHubActions
  - QiitaCLI
private: false
updated_at: '2024-08-31T22:35:17+09:00'
id: 2623c18325968ebe2bbd
organization_url_name: null
slide: false
ignorePublish: false
---
# 1. はじめに
qiita-cliというQiitaの記事をGithub上で管理投稿できるライブラリが公式から用意されています。

https://www.npmjs.com/package/@qiita/qiita-cli?activeTab=readme


ブラウザから投稿すると投稿時にポストするかのモーダルが出てくれますが、qiita-cliからは対応してないみたいなので自動でポストするように改変してみます。
**バージョンの追従が非常にめんどくさくなるのでオススメはしません。**

ここからは企業様のレポジトリをforkして改変していくので一応ライセンス表記

:::note warn
`qiita-cli`は、Apache License 2.0の下でライセンスされています。詳細については、[LICENSEファイル](https://raw.githubusercontent.com/increments/qiita-cli/main/LICENSE.md)を参照してください。
:::

# 2. 環境
・すでにqiita-cliのセットアップが完了しているローカルレポジトリ
・Node.js 18.18.0 以上 （↑がセットアップできているなら入っているはず）

# 3. Twitter API
事前にこちらの記事の手順で各種認証情報を取得しておきます。

https://qiita.com/neru-dev/items/857cc27fd69411496388

必要な情報はこちらの画面の「API Key and Secret」と「Access Token and Secret」です。

![スクリーンショット 2024-08-31 21.18.03.png](https://qiita-image-store.s3.ap-northeast-1.amazonaws.com/0/855584/1457fc4e-7832-c775-7147-9a60cc851dfe.png)

# 4. qiita-cli

## forkしてくる
公式のレポジトリからfrokしました。

https://github.com/increments/qiita-cli

適当にdevというブランチを切りここで作業していきます。

https://github.com/Pokeyama/qiita-cli/tree/dev

## Xにポストする処理
どこに作ってもいいんですが```src/lib/```以下にXにポストするための処理を用意しておきます。
先ほど用意したTwitterAPIの認証情報をここで環境変数から入れます。
```ts:tweet.ts
import { TwitterApi } from "twitter-api-v2";

export const tweet = async (args: string[]) => {
  // 環境変数が設定されているか確認
  const apiKey = process.env.TWITTER_API_KEY;
  const apiSecret = process.env.TWITTER_API_SECRET;
  const accessToken = process.env.TWITTER_ACCESS_TOKEN;
  const accessSecret = process.env.TWITTER_ACCESS_SECRET;

  if (!apiKey || !apiSecret || !accessToken || !accessSecret) {
    console.log("Twitter API keys are not set. Skipping tweet.");
    return;
  }

  // 環境変数が揃っている場合のみ、Twitter APIクライアントを初期化
  const client = new TwitterApi({
    appKey: apiKey,
    appSecret: apiSecret,
    accessToken: accessToken,
    accessSecret: accessSecret,
  });

  const message = args.join(" ");

  try {
    const tweetResponse = await client.v2.tweet(message);
    console.log("Successfully tweeted:", tweetResponse.data);
  } catch (err) {
    console.error("Failed to send tweet:", err);
  }
};
```

## qiita-api
内部的にはqiita-apiを呼び出しているみたいでこちらをラップしているディレクトリが```./qiita-api```以下にあります。

こちらの```POST /api/v2/items```を使って投稿しているみたいです。

https://qiita.com/api/v2/docs#post-apiv2items

このAPIで投稿時のURLがレスポンスに入っているみたいなんですが、qiita-cliでは受け取っているオブジェクトにフィールドが含まれていないので追加します。
この際、null許容で追加しないと予め用意されているテストでエラーが出たので```?```をつけています。

```ts:./src/qiita-api/index.ts
export interface Item {
  body: string;
  id: string;
  private: boolean;
  tags: {
    name: string;
  }[];
  title: string;
  organization_url_name: string | null;
  coediting: boolean;
  created_at: string;
  updated_at: string;
  slide: boolean;
  url?: string; // urlをnull許容で追加
}
```

## 投稿する時にポストする
```src/commands/publish.ts```が投稿するときのロジックっぽいのでこちらに先ほどのポストする処理を追加します。
長いので既存の処理は省略します。

```ts:./src/commands/publish.ts
// ---- 省略
import { tweet } from "../lib/tweet"; // インポート

// ---- 省略
  const promises = targetItems.map(async (item) => {
    let responseItem: Item;
    if (item.id) {
      responseItem = await qiitaApi.patchItem({
        rawBody: item.rawBody,
        tags: item.tags,
        title: item.title,
        uuid: item.id,
        isPrivate: item.secret,
        organizationUrlName: item.organizationUrlName,
        slide: item.slide,
      });

      console.log(`Updated: ${item.name} -> ${item.id}`);
    } else {
      responseItem = await qiitaApi.postItem({
        rawBody: item.rawBody,
        tags: item.tags,
        title: item.title,
        isPrivate: item.secret,
        organizationUrlName: item.organizationUrlName,
        slide: item.slide,
      });
      await fileSystemRepo.updateItemUuid(item.name, responseItem.id);

      // ---- ここを追加 Xにポストする処理を追加
      const tweetMessage = `記事を投稿しました！\n\n${responseItem.title}\n${responseItem.url}\n#Qiita`;
      try {
        console.log(`private is : ${responseItem.private}`);
        if(!responseItem.private){
          await tweet([tweetMessage]);
        }
      } catch (err) {
        console.error("Failed to post on Twitter:", err);
      }
      console.log(`Posted: ${item.name} -> ${responseItem.id}`);
      // ---- 追加ここまで
    }

    await fileSystemRepo.saveItem(responseItem, false, true);
  });

  // ---- 省略
};
```

限定記事の場合、ツイートされないようにしています。（限定記事書かないけど）
```ts
if(!responseItem.private){
  await tweet([tweetMessage]);
}
```

Github Actionの設定をしないならこれでqiita-cliの改変は終わりです。

## Github Action
```git push```時に投稿とポストをしたい場合はこちらも改変していきます。
```npm install -g```している部分を今作成している自分のレポジトリにして**ローカルインストール**におきます。
また、```qiita```コマンドの実行をnpxで行うようにしておきます。

```yml:./actions/publish/action.yml
name: "Publish articles to Qiita"
description: "Publish articles to Qiita using qiita-cli"
author: "Qiita Inc."

inputs:
  root:
    required: false
    default: "."
    description: "Root directory path"
  qiita-token:
    required: true
    description: "Qiita API token"

runs:
  using: "composite"
  steps:
    - uses: actions/setup-node@v4
      with:
        node-version: "20.16.0"
    - name: Install qiita-cli
      # ここを自分のレポジトリにする
      run: npm install github:Pokeyama/qiita-cli#dev
      shell: bash
    - name: Publish articles
      # npxで実行するようにしておく
      run: npx qiita publish --all --root ${{ inputs.root }}
      env:
        QIITA_TOKEN: ${{ inputs.qiita-token }}
      shell: bash
    - name: Commit and push diff # Not executed recursively even if `push` is triggered. See https://docs.github.com/en/actions/using-workflows/triggering-a-workflow#triggering-a-workflow-from-a-workflow
      run: |
        git add ${{ inputs.root }}/public/*
        if ! git diff --staged --exit-code --quiet; then
          git config --global user.name 'github-actions[bot]'
          git config --global user.email '41898282+github-actions[bot]@users.noreply.github.com'
          git commit -m 'Updated by qiita-cli'
          git push
        fi
      shell: bash
```

ここがうまくいかなかった。
なんでかグローバルインストールだとqiitaコマンドが使えなくて苦戦。
色々あってこの形になりました。
ありがとうM平さん。

# 5. qiita-cliをインストールしているレポジトリ
ここからはqiita-cliを使用している記事管理レポジトリを少し改変していきます。

元のqiita-cliをアンインストールして、先ほど改変したレポジトリから```npm install```します。

```
npm uninstall @qiita/qiita-cli   
npm install github:Pokeyama/qiita-cli#dev --save-dev 
```

## .envに認証情報を書く
ルートディレクトリに.envファイルを作成して以下の対応する変数に最初用意したTwitterAPIの認証情報を書いておきます。
**```.gitignore```に追加するの忘れずに。**

```
TWITTER_API_KEY=xxxxxxxxxxx
TWITTER_API_SECRET=xxxxxxxxxxx
TWITTER_ACCESS_TOKEN=xxxxxxxxxxx
TWITTER_ACCESS_SECRET=xxxxxxxxxxx
```

Github Actionを使わないならこの時点で自動ポストができているはずです。

```sh
npx qiita publish {記事名}
```

## Github Action
```git push```時にも投稿したい場合こちらも追記します。

Github上の```Settings > Secrets and variacles > Actions```に```.env```に入れた時と同じTwitterAPIの認証情報を入れます。

![スクリーンショット 2024-08-31 22.15.20.png](https://qiita-image-store.s3.ap-northeast-1.amazonaws.com/0/855584/ca6feecf-e820-49ec-168e-16211ee965a7.png)

ymlもTwitterAPIの認証情報を受け取るように編集します。

```yml:.github/workflows/publish.yml
# Please set 'QIITA_TOKEN' secret to your repository
name: Publish articles

on:
  push:
    branches:
      - main
      - master
  workflow_dispatch:

permissions:
  contents: write

concurrency:
  group: ${{ github.workflow }}-${{ github.ref }}
  cancel-in-progress: false

jobs:
  publish_articles:
    runs-on: ubuntu-latest
    timeout-minutes: 5
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0
      - uses: Pokeyama/qiita-cli/actions/publish@dev
        with:
          qiita-token: ${{ secrets.QIITA_TOKEN }}
          root: "."
        # TwitterAPI認証情報を受け取れるようにしておく  
        env:
          TWITTER_API_KEY: ${{ secrets.TWITTER_API_KEY }}
          TWITTER_API_SECRET: ${{ secrets.TWITTER_API_SECRET }}
          TWITTER_ACCESS_TOKEN: ${{ secrets.TWITTER_ACCESS_TOKEN }}
          TWITTER_ACCESS_SECRET: ${{ secrets.TWITTER_ACCESS_SECRET }}
```

以上で```git pull```時にも投稿されるようになります。

# まとめ
NodeもTypeScriptも素人なんでおかしいところがあるかもしれないです。
特にqiita-cli側のnpm install時にうまくコマンドをうまく利用できなかった理由が今でもわかってないです。
元のレポジトリを弄っちゃっているので、今後はqiita-cliのバージョンに追従するのが大変だなあという印象です。
XのAPIの規約がコロコロ変わるから公式では実装難しいんだろうなって作ってて思いました。
