---
title: Rubyガチ未経験でもJekyllでいい感じの転職活動用自己紹介サイトを作る
tags:
  - ''
private: false
updated_at: ''
id: null
organization_url_name: null
slide: false
ignorePublish: false
---
# はじめに
転職活動を始めたんですが、Rubyの勉強も兼ねてJekyllで自己紹介サイトを作ってみました。
結果的にRubyの勉強には全くならなかったのですが、デザインセンスがなくても簡単にそれっぽく作れたので記事にします。

作った自己紹介サイト

https://pokeyama.github.io/portfolio-jekyll/

この記事の手順でこのサイトとほぼ同等のものが作れます。

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

主に2の理由でJekyllにしました。
デザインセンス皆無なんでMarkdownで書きたかったっていうのが一番大きい理由です。

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

プロジェクトの中のGemfileに以下を記述します。

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
`これはPagesへのデプロイ時に書き換えます。`

```yml
# 元のテーマはコメントアウトしておく しなくても動きます
#theme: minima
theme: "minimal-mistakes-jekyll"
```

### ビルドして確認
`_config.yml`を編集した際は後述のホットリロードの設定をしておいてもビルドが必要になります。
`-l`オプションでホットリロードができるようになるのでつけておきましょう。

```sh
$ bundle exec jekyll serve -l
```

http://localhost:4000/ にアクセス
初期テーマと違うテイストのページに変わっていると思います。

![スクリーンショット 2024-10-02 23.11.30.png](https://qiita-image-store.s3.ap-northeast-1.amazonaws.com/0/855584/5441e974-dacd-b244-cc63-f313fcecb5d9.png)

### Sassの警告がうるさいとき
jekyllのこのバージョンのせいだと思うんですが、Sassの非推奨関数を使っているという警告が無限に出ると思います。
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
このページへのパスになります。
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

#### Tips.ymlは書かなくてもいい
`Front Matter`はjekyllに対して、**jekyllで書かれたページだよ**と教えて上げる意味があります。
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

## _config.yml

### urlとbaseurl
 
## GitHub Pagesにデプロイ

## まとめ