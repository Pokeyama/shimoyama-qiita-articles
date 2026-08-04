---
title: 完全ローカルで会議を「そっと」録音+「そっと」文字起こしするmacOSアプリ「Sotto」を作った
tags:
  - Swift
  - 個人開発
  - macOS
  - ScreenCaptureKit
  - 音声認識
private: false
updated_at: ''
id: null
organization_url_name: null
slide: false
ignorePublish: false
---
# はじめに
2年前に、音声ファイルから議事録を生成するツールを作りました。

https://qiita.com/simoyama2323/items/3833068c2856bb579707

Speech-to-TextとGeminiをつないだCLIで、60分130円かかります。
しかもMacの録音関係はBlackHoleでマイクとシステム音声をMIXする必要があったりでめんどうで作ったのにあまり使っていませんでした。
あとこのツールの問題ですが、音声をGCSにあげるのもなんか微妙です。

macOS Tahoe 26から音声APIに（個人的に）革新的なモノが追加されていたので、メニューバーアプリとして作り直しました。
Sottoといいます。

##### Sottoちゃん
<img src="https://raw.githubusercontent.com/yhrym/Sotto/main/.github/assets/sotto-app-icon-v2.png" width="144" alt="Sottoのアプリアイコン">


https://github.com/yhrym/Sotto

メニューバーのアイコンから`録音を開始`を押すだけで、システム音声とマイクを録って、停止したら端末の中で文字起こしします。

システム音声とマイクが分かれた、時刻付きのMarkdownが出てきます。

**料金は0円で、音声はどこにも上がりません。**

:::note warn
他人の音声を録音する場合は、録音前に対象となる全員から明示的な同意を得てください。
:::

# どう使うか
アプリを起動すると、メニューバーにSottoちゃんが出ます。

<img src="https://raw.githubusercontent.com/yhrym/Sotto/main/.github/assets/sotto-menubar-idle.png" width="80" alt="メニューバーで待機中のSotto">

クリックするとメニューが開きます。

<img src="https://qiita-image-store.s3.ap-northeast-1.amazonaws.com/0/855584/d677bfea-bc79-450a-a2e5-705f23b71f9e.png" width="319" alt="待機中のSottoのメニュー">

あとは`録音を開始`を押すだけです。
録音中は目が赤くなって、横に録音時間が出ます。

<img src="https://qiita-image-store.s3.ap-northeast-1.amazonaws.com/0/855584/374ec9e7-8cc9-4726-b887-121367887332.png" width="325" alt="録音中のSottoのメニュー">

止めると、その日の日付のフォルダに音声とMarkdownが並びます。

```text
~/Music/Sotto/2026-07-28/
├── 2026-07-28_143012.m4a
└── 2026-07-28_143012.md
```

出てくるMarkdownはこういう形です。

```markdown
# 2026-07-28 14:30:12 (52分13秒)

- [00:00:03] システム: おはようございます
- [00:00:07] 自分: よろしくお願いします
```

システム音声を`システム`、マイクを`自分`として、時刻順に並べています。
分けているのは音の入り口だけで、話者分離はしていません。相手が3人いても全員`システム`になります。

スピーカーで会議すると相手の声がマイクにも回り込んで、`システム`と`自分`の両方に載ります。
ヘッドホンかイヤホンを使ったほうがきれいに分かれます。

文字起こしは停止後にバックグラウンドで動くので、待たずに次の録音を始められます。

録音中はアイドルスリープを止めています。
画面は消えていいので、ディスプレイのスリープは止めていません。
止めているのは放置によるスリープだけなので、蓋を閉じたり自分でスリープさせた場合は普通に落ちます。

設定画面でいろいろ設定できるようにしてあります。

<img src="https://qiita-image-store.s3.ap-northeast-1.amazonaws.com/0/855584/8119932c-eb26-4632-8fac-2218d8f09f2f.png" width="556" alt="Sottoの設定画面">

保存先、使うマイク、AACのビットレート、ログイン時の起動、マイクとシステム音声のゲインを変えられます。
`入力チェックを開始`を押すと、録音せずに入力レベルだけ見られます。

# 環境
macOS 26.0以上
**文字起こしを使う場合はAppleシリコンが搭載されているMacが必要です**

macOS 26はIntel Macにも入りますが、`SpeechTranscriber`のモデルがAppleシリコン前提なので、Intel機だと録音までしかできません。

# インストール
Releasesから`Sotto-local.zip`を落とします。

https://github.com/yhrym/Sotto/releases/latest

展開して`Sotto.app`をApplicationsへ移します。
一度開くと警告が出るので、閉じてから`システム設定 > プライバシーとセキュリティ`の「このまま開く」を押します。
警告が出るのはDeveloper IDで署名していないからなので、この手順は初回のみ。

# 以下、過去のmacOSでのネックだった部分
録音そのものは昔からできます。
[`AVAudioRecorder`](https://developer.apple.com/documentation/avfaudio/avaudiorecorder)はmacOS 10.7、[`AVAudioEngine`](https://developer.apple.com/documentation/avfaudio/avaudioengine)は10.10からあって、どちらも時間の制限はありません。

## システム音声が録れなかった
マイクと違って、他のアプリが鳴らしている音は標準APIでは取れませんでした。
BlackHoleのような仮想オーディオデバイスを入れて、出力先をそっちに向ける必要があります。
配布するアプリの前提にはしたくないやつです。

https://github.com/ExistentialAudio/BlackHole

ScreenCaptureKitの[`capturesAudio`](https://developer.apple.com/documentation/screencapturekit/scstreamconfiguration/capturesaudio)がmacOS 13から入って、ここが標準APIだけで済むようになりました。
マイクも同じ`SCStream`から受け取れる[`captureMicrophone`](https://developer.apple.com/documentation/screencapturekit/scstreamconfiguration/capturemicrophone)はmacOS 15からです。

それ以前は、システム音声をScreenCaptureKit、マイクを`AVAudioEngine`で別々に録音する必要がありました。
再生時間の基準が違う2つを組み合わせる必要があって、後述の[2系統は到着順に並べられない](#2系統は到着順に並べられない)を面倒な条件でやることになります。

## 長い音声の文字起こしができなかった
Appleの音声認識自体は昔からあります。
`SFSpeechRecognizer`はmacOS 10.15からあって、オンデバイス認識もできました。
ただ、ドキュメントには音声の長さについてこう書かれています。

> Plan for a one-minute limit on audio duration.
> （[SFSpeechRecognizer | Apple Developer Documentation](https://developer.apple.com/documentation/speech/sfspeechrecognizer)）

**録音の制限ではなく、認識に投げられる長さの制限です。**
会議1本をそのまま渡す前提のAPIではありませんでした。

そこにmacOS 26で`SpeechAnalyzer`と`SpeechTranscriber`が入りました。
音声ファイルを最後まで流し込んで、端末内で認識できます。
ドキュメントに長さの制限の記載はなく、実際に120分の録音を分割せずそのまま起こすことができました。

https://developer.apple.com/documentation/speech/speechanalyzer

残っていた文字起こしがとても楽になったので、このタイミングで作りました。

# ハマったポイント

## 音だけ録りたいのに映像ストリームが要る
ScreenCaptureKitで音を録るには`SCStream`を作るのですが、macOS 26で試したところ`.audio`と`.microphone`だけを追加したストリームからはサンプルが1つも流れてきませんでした。
`.screen`の出力も追加すると動きます。

映像は使わないので、2x2ピクセル、1秒に1フレームまで削っています。
`queueDepth`もドキュメントの推奨範囲（3〜8）から外れる1にしていますが、捨てる映像を溜める意味がないので意図的に最小です。

```swift
configuration.width = 2
configuration.height = 2
configuration.minimumFrameInterval = CMTime(seconds: 1, preferredTimescale: 600)
configuration.queueDepth = 1
configuration.capturesAudio = true
configuration.captureMicrophone = true
configuration.excludesCurrentProcessAudio = true
```

届いた映像サンプルは、コピーもせずそのまま捨てています。

```swift
case .screen:
    // ScreenCaptureKit requires a video stream; never retain or copy it.
    return
```

## マイクは指定したフォーマットで来ない
`sampleRate = 48_000`と`channelCount = 2`を指定しても、マイク側はそれと違う実フォーマットで届くことがあります。
システム音声とマイクで別々のものが来るので、両方を48 kHz / 2chのFloat32へ変換してから合成しています。

実際に届いたフォーマットは、系統ごとに最初の1回だけログに残しています。

```swift
logger.info(
    "\(source.rawValue) input format: \(asbd.pointee.mSampleRate) Hz, \(asbd.pointee.mChannelsPerFrame) ch"
)
```

残すのはサンプルレートとチャンネル数だけです。
音声の中身や録音対象の名称は書きません。

## PCMのコピーが0バイトになる
`CMSampleBuffer`から`AVAudioPCMBuffer`へPCMをコピーする箇所です。
`AVAudioPCMBuffer`は`frameLength`が0の間、`mutableAudioBufferList`のバッファを`mDataByteSize = 0`で返してきます。
コピーするバイト数を`min(送り元, 送り先)`で決めていたので、この0を拾って`memcpy`が0バイトになります。

エラーは出ません。**無音のm4aが普通に出来上がります。**

先に`frameLength`を入れておけば直ります。

```swift
guard let sourceBuffer = AVAudioPCMBuffer(
    pcmFormat: sourceFormat,
    frameCapacity: inputFrameCount
) else {
    throw AudioNormalizationError.allocation
}
// この1行がないと下のコピーが0バイトになる
sourceBuffer.frameLength = inputFrameCount
```

## AVAudioConverterをコールバックごとに作らない
`AVAudioConverter`はチャンクをまたいで使い回しています。
毎回作り直すと、リサンプリングの端数がそのたびに捨てられて、長く録るほど時刻がずれていきます。
入力フォーマットが変わったときだけ作り直して、`primeMethod`は`.none`にしました。

```swift
newConverter.primeMethod = .none
```

## 2系統は到着順に並べられない
システム音声とマイクは別々のキューで飛んできます。
到着順に書き込むと、喋った順とずれます。

なので`CMSampleBuffer.presentationTimeStamp`を48 kHzのフレーム位置に直して、絶対位置で並べています。

```text
frameIndex = round((PTS - epoch) × 48,000)
```

### 片方だけ来ていない区間をどうするか
両方揃っている範囲はそのまま出します。
片方だけ来ていない区間は、100ミリ秒待って来なければ無音として確定します。

```text
             確定して書き出し済み        ←100ms→    まだ待つ
system  ████████████████████████████████│░░░░░░░░░░░░
mic     ██████████████████              │░░░░░░░░░░░░
                          ↑
                    ここは無音で埋めて確定する
```

確定したあとに遅れて届いた分は捨てます。
差し込むと、すでに`AVAssetWriter`へ渡した時刻がずれるためです。

### 出力は3本ある
ミックスしたものだけ作ると、あとでどちらが喋ったのか分けられません。
なので合成前のPCMを分岐させて、3本のwriterに配っています。

```text
system PCM ─┬─→ ミキサー ─→ 保存用の .m4a（ミックス済み）
            └─→ システム音声だけの一時ファイル

mic PCM ────┬─→ ミキサー
            └─→ マイクだけの一時ファイル
```

文字起こしがOFFのときは、一時ファイル側の購読者を作りません。

## AirPodsに切り替えると録音が止まる
録音中に入力デバイスが変わると`SCStream`が止まります。
止まるたびに録音終了だと使い物になりません。

なので停止した原因ごとにexit処理を分けました。

```text
recording
  ├─ 録音停止を押した
  │    → stopping → finalizing → 文字起こしへ → idle
  │
  └─ ストリームが落ちた（デバイス変更など）
       → rollingOver
          → いまのファイルを閉じる
          → 次のセグメントで再開 → recording
```

落ちたらファイル名を`_part02`に送って録り直します。

```text
2026-07-28_143012.m4a
2026-07-28_143012_part02.m4a
```

セグメントごとに文字起こしして、同じ名前で`.md`を出します。

### デバイスが変わる瞬間はPTSが飛ぶ
飛んだぶんを無音で埋めると、1回のコールバックで数十秒ぶんのPCMを作ることになります。

なので1コールバックで作るPCMは2秒までにして、超えたらそのチャンクごと取り消すようにしました。
連続している範囲だけ確定して、ファイルを分けて再開する側に倒しています。

```swift
/// Bounds the number of PCM frames materialized by one callback. A larger
/// discontinuity is treated as a route-change failure and triggers rollover.
var maximumBatchDuration: TimeInterval = 2
```

### Bluetoothイヤホンのマイクを掴むと再生音質が落ちる
Macユーザーは経験あると思うのですが、Bluetoothイヤホンのマイクを使っている状態でYouTubeなどを見ると、音声がラジオっぽくなります。

イヤホンのマイクが要求された時点で、再生専用の高音質プロファイル（A2DP）から通話用の双方向プロファイル（HFP）へ切り替わるからです。
HFPは帯域が狭いので、マイクだけでなく**再生側の音質も一緒に落ちます**。
Sottoはシステム音声も録っているので、録音物にもその劣化した音がそのまま入ります。

Bluetoothの規格の話でこちらではどうにもならないので、設定で使うマイクを選べるようにしました。
Mac内蔵マイクや有線マイクを選べば起きません。

# 通信していないことをどう見せるか
音声を外に出さないのが目的なので、「出していません」と書くだけでは弱いです。
そもそも通信できない状態にして、それを読んだ人が確認できるようにしました。

App Sandboxを有効にして、持っているentitlementはこれだけです。

```xml
<key>com.apple.security.app-sandbox</key>
<true/>
<!-- マイク -->
<key>com.apple.security.device.audio-input</key>
<true/>
<!-- ~/Music/Sotto への保存 -->
<key>com.apple.security.assets.music.read-write</key>
<true/>
<!-- 保存先を変えたとき -->
<key>com.apple.security.files.user-selected.read-write</key>
<true/>
<!-- 変えた保存先を次回起動でも覚えておくため -->
<key>com.apple.security.files.bookmarks.app-scope</key>
<true/>
```

`com.apple.security.network.client`と`com.apple.security.network.server`がありません。
サンドボックスはコード署名から適用されるので、コードにどう書いてあろうとソケットを開けません。
外部API、SPMパッケージ、テレメトリ、自動更新も入れていません。

## 例外はモデルのダウンロードだけ
端末に文字起こし用の日本語モデルがないとき、初回だけダウンロードが走ります。

ただしこれはSottoが通信しているわけではありません。
`AssetInventory.assetInstallationRequest(supporting:)`でOSにリクエストを投げると、ダウンロードはAppleのアセットデーモン側で行われます。
Sotto自身のプロセスは、このときもネットワークを触りません（触れません）。

## 自分で確認する
インストールしたアプリのentitlementはこれで見られます。

```bash
codesign -d --entitlements - /Applications/Sotto.app
```

実際にソケットを持っていないかは、録音から文字起こし完了までこれを流したままにすれば分かります。

```bash
while sleep 1; do lsof -nP -a -p "$(pgrep -x Sotto)" -i; done
```

1回叩くだけだとその瞬間しか見ていないので、回しっぱなしにして何も出てこないことを見るほうが確実です。

CI側でも、Releaseをビルドしたあとにentitlementの集合が想定と一致するか比較して、違ったら失敗させています。

```bash
if [[ "$actual_entitlements" != "$expected_entitlements" ]]; then
  echo "Unexpected entitlement set."
  exit 1
fi
```

# まとめ
BlackHoleを使わずにMIXできればなーとずっと思っていたので完成してよかったです。
咄嗟の会議のとき音声周りでトラブルことが何度あったか。。。（それで使わなくなった）

macOS 26のAppleシリコン機を使っているなら、落としてすぐ使えるのでぜひ使ってみてください。

https://github.com/yhrym/Sotto

# 参考
https://developer.apple.com/documentation/speech/speechanalyzer

https://developer.apple.com/documentation/speech/speechtranscriber

https://developer.apple.com/documentation/screencapturekit

https://developer.apple.com/documentation/screencapturekit/scstreamconfiguration/capturesaudio

https://developer.apple.com/documentation/screencapturekit/scstreamconfiguration/capturemicrophone
