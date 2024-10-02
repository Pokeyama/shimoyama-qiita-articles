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

```sh
$ bundle exec jekyll serve
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

### ホットリロード



## ページを作っていく

## _config.yml

### urlとbaseurl
 
## GitHub Pagesにデプロイ

## まとめ