---
title: UNIQLOのTシャツのコードを実行しようとしたら意図せずGO言語に入門した
tags:
  - Go
  - HelloWorld
private: false
updated_at: '2024-08-04T21:27:55+09:00'
id: 62c3f7e5ff78b3156af5
organization_url_name: null
slide: false
ignorePublish: false
---
# はじめに
UNIQLOのバラエティゾーンで売ってるプログラムが書かれたTシャツありますよね。
そもそもGOで書かれているとすら知らなかったのですが、GOについて何も知らない状態から動かしてみました。
筆者は普段C#とPHPをメインに使用していて、GOは全くの素人です。
意図せずGOのHelloWorldみたいになったので記事にします。

--- 

とりあえず実行してみたい方は以下のレポジトリで実行できます。
Dockerが入っていればGOが入ってなくても動きます。

https://github.com/Pokeyama/uniqlo-tshirt-app

# シャツとコード
<img width="300" alt="uniqlo-shirt.jpg" src="https://qiita-image-store.s3.ap-northeast-1.amazonaws.com/0/855584/5bb3f1e8-9f58-6866-af22-aa2e4aa33ca4.jpeg">

iPhoneで撮ると勝手にOCRして文字として読み取ってくれたので、そのままGPTに整形してもらって軽く解説してもらいました。

```Go
package main

import (
	"fmt"
	"html"
	"net/http"
	"strconv"
	"strings"
	"time"
)

type ControlMessage struct {
	Target string
	Count  int
}

func main() {
	controlChannel := make(chan ControlMessage)
	workerCompleteChan := make(chan bool)
	statusPollChannel := make(chan chan bool)
	workerActive := false

	go admin(controlChannel, statusPollChannel)

	for {
		select {
		case respChan := <-statusPollChannel:
			respChan <- workerActive
		case msg := <-controlChannel:
			workerActive = true
			go doStuff(msg, workerCompleteChan)
		case status := <-workerCompleteChan:
			workerActive = status
		}
	}
}

func admin(cc chan ControlMessage, statusPollChannel chan chan bool) {
	http.HandleFunc("/admin", func(w http.ResponseWriter, r *http.Request) {
		hostTokens := strings.Split(r.Host, ":")
		r.ParseForm()
		count, err := strconv.ParseInt(r.FormValue("count"), 10, 32)
		if err != nil {
			fmt.Fprint(w, err.Error())
			return
		}

		msg := ControlMessage{
			Target: r.FormValue("target"),
			Count:  int(count),
		}
		cc <- msg

		fmt.Fprintf(w, "Control message issued for Target %s with Count %d", html.EscapeString(r.FormValue("target")), msg.Count)
	})

	http.HandleFunc("/status", func(w http.ResponseWriter, r *http.Request) {
		reqChan := make(chan bool)
		statusPollChannel <- reqChan
		timeout := time.After(1 * time.Second)

		select {
		case result := <-reqChan:
			if result {
				fmt.Fprint(w, "ACTIVE")
			} else {
				fmt.Fprint(w, "INACTIVE")
			}
		case <-timeout:
			fmt.Fprint(w, "INACTIVE")
		}
	})

	http.ListenAndServe(":8080", nil)
}

func doStuff(msg ControlMessage, workerCompleteChan chan bool) {
	// Do some work here
	time.Sleep(time.Duration(msg.Count) * time.Second)
	workerCompleteChan <- false
}
```

以下の部分から8080でサーバーが起動して、/adminと/statusエンドポイントがあるサーバー側のコードなのかなということ以外このときよくわからず。
```go
http.HandleFunc("/admin", func(w http.ResponseWriter, r *http.Request) {}
http.HandleFunc("/status", func(w http.ResponseWriter, r *http.Request) {}
```

```go
http.ListenAndServe(":8080", nil)
```

あと、元のコードより長くないか？と思ったらdoStuff()という関数が生えていました。
足りない処理を追加してくれたみたいです。

解説も聞いてみました。
```text:ChatGPT
このコードは、制御メッセージの受け渡しと、ワーカーの状態の問い合わせを行うシンプルなサーバーを実現しています。
```
うん。よくわかりません。（ワーカー？スレッドのことかな？）
よくわからないまま修正されても何が変わったのか理解できないので、とりあえずdoStuff()関数は消して実行できる状態を目指します。

# 実行までの道のり
自分の環境にGOを入れたくないのでDockerで動かしました。

## Docker
```Dockerfile
# ベースイメージとして公式のGoイメージを使用
FROM golang:1.20

# 作業ディレクトリを設定
WORKDIR /app

# Goモジュールのキャッシュを利用するため、go.modとgo.sumをコピー
COPY go.mod ./

# 依存関係をダウンロード
RUN go mod download

# ソースコードをコピー
COPY . .

# アプリケーションをビルド
RUN go build -o main .

# ポート8080を開放
EXPOSE 8080

# コンテナ起動時に実行されるコマンド
CMD ["./main"]
```

GPTに最初提示されたDockerfileではgo.sumをCOPYする記述があって作れと言われました。
PHPのcomposer.lockみたいな実行時の依存性のバージョンを管理するファイルですかね。
以下のコマンドで生成できるとあったのですが、そもそも今回のコードだと何も依存していないとのことで生成されませんでした。
なので記述から削除しています。

```shell
docker run --rm -v "$PWD":/app -w /app golang:1.20 go mod tidy
```

## Go
go.modという依存性を管理するファイルが必要とのことで追加。
go.sumが生成されなかったのはここで依存性が何も書かれていなかったからと予想。

```go
module myapp

go 1.20
```

## ビルドエラー
以下のコマンドでビルド
```shell
docker build -t uniqlo-shirt-app .
```

使っていない変数msgがあるというエラー
```log
------
 > [6/6] RUN go build -o main .:
2.930 # myapp
2.930 ./main.go:28:8: msg declared and not used
------
```

GOってビルド時に使っていない変数があるとエラーになるのか。
めっちゃよきですね。

以下のようにGPTにmain()の修正してもらう。
doStuff()のところですね。
記述されていない関数指定しているしそりゃそうだ。

```go
func main() {
	controlChannel := make(chan ControlMessage)
	workerCompleteChan := make(chan bool)
	statusPollChannel := make(chan chan bool)
	workerActive := false

	go admin(controlChannel, statusPollChannel)

	for {
		select {
		case respChan := <-statusPollChannel:
			respChan <- workerActive
		case <-controlChannel: // msg変数を使用しない場合
			workerActive = true
			workerCompleteChan <- false
		case status := <-workerCompleteChan:
			workerActive = status
		}
	}
}
```

_でこねこねすると回避できるみたいです。

https://go.dev/doc/effective_go#blank

ビルドエラーはこれで解消。

## 実行時エラー
以下のコマンドで実行
```shell
docker run --rm -p 8080:8080 uniqlo-shirt-app
```

GPTにcurlで試したい旨伝えたら以下のコマンドを提示される。

```shell
curl -X POST "http://localhost:8080/admin" -d "target=mytarget&count=5"
curl "http://localhost:8080/status"
```

このへんからどんなコードなのかを理解してきました。
/adminでformのcountフィールドを受け取って何かしてそうです。

リクエストを送ってみたら接続先エラー
```shell
$ curl "http://localhost:8080/status"

curl: (56) Recv failure: Connection reset by peer
```

サーバーの処理が間違っていそうです。
GPTに修正してもらう。

```go
func main() {
	controlChannel := make(chan ControlMessage)
	workerCompleteChan := make(chan bool)
	statusPollChannel := make(chan chan bool)
	workerActive := false

	go admin(controlChannel, statusPollChannel)

	for {
		select {
		case respChan := <-statusPollChannel:
			respChan <- workerActive
			log.Println("Status requested, responded with:", workerActive)
		case msg := <-controlChannel:
			workerActive = true
			log.Println("Received control message:", msg)
			// Simulate some work
			go func() {
				time.Sleep(time.Duration(msg.Count) * time.Second)
				workerCompleteChan <- false
				log.Println("Work complete for target:", msg.Target)
			}()
		case status := <-workerCompleteChan:
			workerActive = status
			log.Println("Worker status updated to:", workerActive)
		}
	}
}
```

先ほどのdoStuff()のところに似たような関数が追加されました。
time.Sleep()ということは処理をここでcount秒止めているのか？と予想。

## 動いた！
とりあえず実行してエラーが解消したこと確認。
```shell
$ curl -X POST "http://localhost:8080/admin" -d "target=mytarget&count=10"
Control message issued for Target mytarget with Count 10%                     
$ curl "http://localhost:8080/status"
ACTIVE%                                
$ curl "http://localhost:8080/status"
INACTIVE%
```

/adminに10秒待てというリクエストを送り、/statusで現在スレッド（表現が違うかもしれない）が待ち状態かどうかをレスポンスするコードだと予想。

# 結局どんなコードだったのか
以上の動きからC#er的には完全に並列処理な気はして身構えたんですが、たぶん以下についてもっと理解しないと本質的な理解はできなそうです。
1. ゴルーチン
2. チャネル

以下から少し勉強した内容を書きます。
多分に間違いを含むと思うので、多めに見て頂きたく。
## ゴルーチン
以下の部分で単一のゴルーチンが生成されてる。
C#er的にはスレッドが生成されているイメージかなと思います。
```go
go admin(controlChannel, statusPollChannel)
```

ここのスレッドでactiveかどうかのフラグを単一管理していている。
さらに以下のゴルーチンが都度生成されてフラグを切り替えている。

```go
		case msg := <-controlChannel:
			workerActive = true
			log.Println("Received control message:", msg)
			// ワーカーが処理を開始する
			go func() {
				time.Sleep(time.Duration(msg.Count) * time.Second)
				workerCompleteChan <- false
				log.Println("Work complete for target:", msg.Target)
			}()
```

複数ゴルーチンがここで立ち上がって並列処理しているが、あくまでadminゴルーチンは単一なので単一性が保たれている。

## チャネル
以下の部分でチャネルなるものを作っていて、これでゴルーチンと変数をやり取りしている。
ポインタみたいな動きに見える。
```go
	controlChannel := make(chan ControlMessage)
	workerCompleteChan := make(chan bool)
	statusPollChannel := make(chan chan bool)
```

# まとめ
部屋着でボロボロのTシャツに書かれていたコードを実行するだけなのに、GOの勉強までしてしまいました。
調べていたらゴルーチンってGOを使う上で当たり前のことらしく、この言語の難しさを理解できました。
なお仕事で使う機会はない模様。

# 参考

https://www.ariseanalytics.com/activities/report/20221005/
