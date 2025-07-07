<!-- ---
title: Ruby未経験でもできる！Jekyllで転職活動用の自己紹介サイトを作成する
tags:
  - Ruby
  - Jekyll
  - 転職
  - ポートフォリオ
private: true
updated_at: '2025-07-07T20:41:14+09:00'
id: 19e2ede05b76af50924e
organization_url_name: null
slide: false
ignorePublish: false
---
# はじめに
転職活動を始めたんですが、忙しくて時間が取れないので少しでもオートマチックに活動ができるようにパブリックな自己紹介サイトを作りました。
あわよくばRubyの勉強になればと思い、Jekyllを使いました。
結果的にRubyの勉強には全くならなかったのですが、デザインセンスがなくても簡単にそれっぽく作れたので記事にします。

作った自己紹介サイト

https://pokeyama.github.io/portfolio-jekyll/

とても長い記事になってしまったのですが、この記事の順を追ってコマンドを実行していけばほぼ同等のものが作れます。

# 環境
Mac M2
Ruby
```sh
$ ruby -v
ruby 3.3.5 (2024-09-03 revision ef084cc8f4) [arm64-darwin23]
```

# Jekyllとは
1. Rubyで開発された静的サイトジェネレーター
2. **Markdownで書ける**
3. GitHub Pagesに割と簡単にデプロイできる。

Jekyllは、ブログやポートフォリオ、技術ドキュメント、個人ウェブサイトなど、簡単なウェブサイトを素早く構築できるのが特徴です。
サーバーサイドの処理が必要ない静的サイトジェネレーターなので、ホスティングコストが低く、特にGitHub Pagesを使えば無料で公開できます。

# 作っていく
## RubyとJekyllのインストール
Mac前提です。
他言語と同じでインストールしてPATHを通すだけ。

```sh
# Ruby
$ brew install ruby
$ echo export PATH="/opt/homebrew/opt/ruby/bin:$PATH" > ~/.zshrc
# jekyll
$ gem install bundler jekyll
$ jekyll -v
jekyll 4.3.4
```

`gem`って`composer`とか`npm`みたいな依存関係管理してくれるものだと思うんですが、このインストール方法だとグローバルっぽいのでちゃんと範囲決めたりDocker使ったほうがいいかもですね。

## Jekyllサイトを作成
Jekyllがインストールできたら早速作っていきましょう。
`jekyll new {プロジェクト名}`で新しいディレクトリができてよしなに作ってくれます。

```sh
$ jekyll new qiita-jekyll
$ cd qiita-jekyll
```

### Hello World（飛ばしてもいい）
とりあえずこの時点でHello Worldしたい方は以下のコマンドでインストール&ビルド&実行をします。
`bundle install`は、GemFileに記載された依存関係をインストールします。

```sh
$ bundle install
$ bundle exec jekyll serve
```

http://localhost:4000/ にアクセスするとトップページに飛べます。

![スクリーンショット 2024-10-02 22.51.48.png](https://qiita-image-store.s3.ap-northeast-1.amazonaws.com/0/855584/ae778273-5f7e-5ab6-2582-1af59380e11f.png)

## minimal-mistakesテーマのインストール
そのままでも自己紹介サイトくらいなら十分作れそうですが、テーマを入れてみます。

https://mmistakes.github.io/minimal-mistakes/

画像をいい感じに貼れたり、全体的な色もざっくり変えられるテーマです。
なんだかこんな感じのページよく見るなあと思って採用しました。

プロジェクトの中のGemFileに以下を記述します。

```ruby
source "https://rubygems.org"

gem "jekyll", "~> 4.3.4"
gem "minimal-mistakes-jekyll", "~> 4.24.0"
```

テーマは更新頻度高くないみたいでjekyll自体の最新バージョンに追いついていないみたいなので、ちゃんとバージョン指定してあげます。
依存関係を追加したのでもう一度インストールします。

```sh
$ bundle install
```

プロジェクト内にある`_config.yml`にもテーマを追記します。
[これはPagesへのデプロイ時に書き換えます。](#theme)

```yml
# 元のテーマはコメントアウトしておく しなくても動きます
#theme: minima
theme: "minimal-mistakes-jekyll"
```

### ビルドして確認
`-l`オプションでホットリロードができるようになるのでつけておきましょう。
`_config.yml`を編集した際は後述のホットリロードの設定をしておいてもビルドが必要になります。
`--baseurl-""`がついている理由は[こちら](#urlとbaseurl)

```sh
$ bundle exec jekyll serve --baseurl="" -l
```

http://localhost:4000/ にアクセス
初期テーマと違うテイストのページに変わっていると思います。

![スクリーンショット 2024-10-02 23.11.30.png](https://qiita-image-store.s3.ap-northeast-1.amazonaws.com/0/855584/5441e974-dacd-b244-cc63-f313fcecb5d9.png)

### Sassの警告がうるさいとき
jekyllのバージョンのせいだと思うんですが、Sassの非推奨関数を使っているという警告が無限に出ると思います。
ログに流したくないときは`_config.yml`に以下を記述します。

```yml
sass:
  quiet_deps: true
```

再ビルドして実行すれば警告が出力されなくなります。

## ページを作っていく
### Front Matter
jekyllは基本的にMarkdownで記述していくのですが、それぞれのページに`Front Matter`という`---`で囲んだyml形式のフィールドがあります。
ここには変数だったりテーマごとの設定を書くことができます。
mistakesではいい感じに画像を配置してくれる設定があるので使ってみましょう。

ざっくり以下2点は覚えておいたほうがいいフィールドです。
1. `layout`
こちらにあるレイアウトが使えます。

https://mmistakes.github.io/minimal-mistakes/docs/layouts/

2. `permalink`
ページへのパスになります。
このページはトップページなので`/`です。

```yml:index.markdown
---
layout: single
title: "ここがタイトル"
date: 2024-09-25
permalink: /
header:
  overlay_image: https://user0514.cdnw.net/shared/img/thumb/aig-ai23419012-xl_TP_V.jpg
  overlay_filter: 0.5
  overlay_color: "#000"
  caption: "ここがキャプション"
  actions:
    - label: "ここがリンク"
      url: https://qiita.com/
      class: "btn--primary"
excerpt: "ここが概要"
---
# ここが記事
## ここが記事
### ここが記事
#### ここが記事
##### ここが記事
```

![スクリーンショット 2024-10-03 22.59.43.png](https://qiita-image-store.s3.ap-northeast-1.amazonaws.com/0/855584/6271b73b-e2d5-459c-857b-ed420af8cc35.png)

一気によく見るホームページのようになりましたね。
あとはMarkdownでどんどんページを書いていきましょう。

#### Tips.ymlの中身は空でもいい
`Front Matter`はjekyllに対して、**jekyllで書かれたページだよ**と教えて上げる意味もあります。
なので以下のように何も書かなくてもフィールドさえ作っておけば以下のようにjekyllがページを作ってくれます。


```yml
---
# 空でもページは作ってくれる。
---
```

`layout`を使わずすべて自前で書くときは使うといいかもしれません。
jekyll使う意味もなくなってしまうかもしれません。

## ページを追加する
トップページができたのでページを追加していきます。
ルートディレクトリに適当な名前でmdファイルを作ります。

```sh
$ touch skill.md
```

中身にトップページと同じで`Front Matter`を記載してあげます。
`permalink`は`/skill`とでもしておきましょう。

```yml:skill.md
---
layout: single
title: "スキル"
permalink: /skill
---
# 私のスキル
## 私のスキル
### 私のスキル
#### 私のスキル
##### 私のスキル
```

[http://localhost:4000/skill](http://localhost:4000/skill)にアクセスしてみましょう。
ページができているはずです。

![スクリーンショット 2024-10-03 23.17.37.png](https://qiita-image-store.s3.ap-northeast-1.amazonaws.com/0/855584/ac2c9bc1-60a2-00e1-0ce7-b7623c29f154.png)

## ヘッダーにページ一覧を表示
ページは追加できましたが、毎回トップページにリンクを貼るのは面倒です。
ヘッダーに一覧を出しちゃいましょう。
ルートディレクトリに`_data/navigation.yml`を作成します。

```sh
$ mkdir _data
$ touch navigation.yml
```

中身にヘッダーに追加したいページタイトルと`permalink`を書きます。
```yml:navigation.yml
main:
  - title: "プロフィール"
    url: /

  - title: "スキル"
    url: /skill
```
![スクリーンショット 2024-10-03 23.23.01.png](https://qiita-image-store.s3.ap-northeast-1.amazonaws.com/0/855584/03d7d972-92eb-9275-64ca-d3e8d00a0712.png)
ヘッダーにリンクが追加されました。

## サイドバーにプロフィールを表示
`_config.yml`にauthorフィールドを追加し、サイドバーにプロフィールを表示します。

```yml:_config.yml
author:
  name: "しもやま"
  bio: "Qiita記事執筆中"
  location: "東京都"
  email: "qiita@example.com"
  linkedin: "https://www.linkedin.com/in/qiita"
  github: "https://github.com/qiita"
```

プロフィールを追加したいページの`Front Matter`に`author_profile`を追加します。

```yml:index.markdown
---
layout: home
title: "ここがタイトル"
date: 2024-09-25
permalink: /
header:
  overlay_image: https://user0514.cdnw.net/shared/img/thumb/aig-ai23419012-xl_TP_V.jpg
  overlay_filter: 0.5
  overlay_color: "#000"
  caption: "ここがキャプション"
  actions:
    - label: "ここがリンク"
      url: https://qiita.com/
      class: "btn--primary"
excerpt: "ここが概要"
author_profile: true # これを追加
---
```

![スクリーンショット 2024-10-05 22.31.01.png](https://qiita-image-store.s3.ap-northeast-1.amazonaws.com/0/855584/ffe62e67-7f63-4cb1-ae60-142d91c494da.png)

### カスタムリンク
カスタムのリンクやSNSを追加したい場合は、`links`フィールドに新しいエントリを追加して表示させることができます。
以下のアイコンサイトの画像がアセットに入っており使用することができます。

https://fontawesome.com/v5/search

注意点があり、こちらのサイトのバージョンが6系のアイコンは使えませんでした。
探すときはバージョンを5系にしましょう。

```yml
author:
  name: "しもやま"
  bio: "Qiita記事執筆中"
  location: "東京都"
  links:
  - label: " Qiita"
    icon: "fas fa-pen" # 代替アイコンとしてペンを使用
    url: "https://qiita.com/simoyama2323"
```


# GitHub Pagesにデプロイ
## GitHub側
ある程度ページができてきたらGitHub Pagesにデプロイしてみましょう。
GitHubにレポジトリを作ってpushしておきます。（省略）
レポジトリができたら、GitHubの設定ページから`Settings` -> `Pages`に移動し、`Branch`の項目を`master`または`main`に設定して`Save`をクリックします。

![スクリーンショット 2024-10-04 16.30.10.png](https://qiita-image-store.s3.ap-northeast-1.amazonaws.com/0/855584/4d4cf5e7-107f-37ae-39b4-cedf005ccc80.png)

GitHub側の設定はこれだけになります。
jekyll用のactionが用意されていて、あとはpushするだけで自動でやってくれます。
超簡単。

## ローカル側
### _config.yml
最後にjekyllの`_config.yml`にPages用の設定をしていきます。

#### urlとbaseurl
GitHub Pagesにデプロイする際、`url`と`baseurl`を正しく設定する必要があります。
`url`は公開するページの基本URLで、`baseurl`はそのURLのサブディレクトリに当たります。

```yml:_config.yml
baseurl: "/{レポジトリ名}"
url: "https://{自分のアカウント名}.github.io"
```

`baseurl`を設定すると、ローカル環境でのビルドでもURLのパスに`/リポジトリ名`が付いてしまいます。
これにより、リンクが正しく機能しない場合があるため、ローカル開発時には`--baseurl=""`オプションを使ってこの挙動を無効にします。
こうすることで、ローカル環境ではトップディレクトリからアクセスできます。

```sh
$ bundle exec jekyll serve --baseurl="" -l
```

#### theme
[こちら](#minimal-mistakesテーマのインストール)で設定したテーマをリモートから取得するように書き換えます。
こうしておかないとGitHubAction内で立ち上がったDocker内からテーマを探しに行ってしまうのでエラーになります。

```yml:_config.yml
# theme: "minimal-mistakes-jekyll"
remote_theme: "mmistakes/minimal-mistakes"
```

#### plugins
リモートテーマを使用するためにプラグインを追加しておきます。

```yml:_config.yml
plugins:
  - jekyll-feed
  - jekyll-remote-theme
  - jekyll-include-cache
```

### GemFile
リモートテーマプラグインを使用するためにGemFileにモジュールをインストールしておきます。

```ruby:GemFile
gem "jekyll-remote-theme"
```

## ページを確認してみよう！
ここまでで最低限の設定は終わりです。
pushするとGitHub Actionが動くのでそれが終わったらページを確認してみましょう。
URLは`https://{ユーザー名}.github.io/{レポジトリ名}/`です。
お疲れ様でした。

## _config.ymlのアレコレ
最低限Pagesで動かすようの設定をしましたが色々な記述ができます。
以下にこのテーマで使えるプロパティの一覧があるので色々試してみるとおもしろいと思います。

https://github.com/mmistakes/minimal-mistakes/blob/master/_config.yml

# まとめ
この記事では、Jekyllを使って転職活動用の自己紹介サイトを作成する手順を紹介しました。
最初は自分で借りてるVPSで公開しようと思ってたんですが、Pagesでの公開が簡単すぎたので使ってみました。
Markdownで書けるって素晴らしい。

# 最後に
この記事が役に立った、または私の技術に興味を持っていただけた企業様がいらっしゃいましたら、ぜひ以下の自己紹介サイトよりお気軽にご連絡ください！  

https://pokeyama.github.io/portfolio-jekyll/

# 使用させて頂いた素材

https://www.pakutaso.com/20230556126post-46690.html

https://icons8.jp/icons/set/docker-image -->
