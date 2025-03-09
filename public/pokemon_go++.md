---
title: Pokémon GO Plus+ の振動を無効化する
tags:
  - 'DIY'
  - 'ポケモン'
private: false
updated_at: ''
id: null
organization_url_name: null
slide: false
ignorePublish: false
---
# はじめに
:::note alert
以下の手順を実施することで、機器の故障やデータ損失（Sleepでのピカチュウのなつき状態）のリスクがあります。
この記事は研究目的での作業なので、もし再現する場合は自己責任で行ってください。
:::

GO++便利ですよね。
モンボ+のころにあったペアリングの頓挫さがなくなり、Sleepでの睡眠計測でも使えるのでポケモンアプリユーザーには欠かせないデバイスだと思います。

唯一の欠点として電車や会社で使用できないほど、振動がうるさすぎるという問題があります。
モンボ+のころからですがこれを切るオプションはないそうです。

[「Pokémon GO Plus +」 の振動しないようにするための方法を知りたい](https://pgpp.pokemon-support.com/hc/ja/articles/20331470722329--Pok%C3%A9mon-GO-Plus-%E3%81%AE%E6%8C%AF%E5%8B%95%E3%81%97%E3%81%AA%E3%81%84%E3%82%88%E3%81%86%E3%81%AB%E3%81%99%E3%82%8B%E3%81%9F%E3%82%81%E3%81%AE%E6%96%B9%E6%B3%95%E3%82%92%E7%9F%A5%E3%82%8A%E3%81%9F%E3%81%84)

:::note info
『Pokémon GO』で使用する場合
・ポケモンの通知やポケストップの通知において、振動を切ることはできません。
:::

今回はこちらを無振動化していきます。

# 分解手順

## 1. 外装の取り外し
赤丸部分のネジを取ります。
形状は＋なのですが、小さめなので精密ドライバーを使用しないと難しいと思います。
<img width="400" src="https://qiita-image-store.s3.ap-northeast-1.amazonaws.com/0/855584/4c5eab67-a3e4-4c05-9cf3-4ada30c29e05.jpeg">

ネジを外したあとは白い部分と黒いふち部分の間をヘラで抉っていきます。

**注意**
青丸部分に振動防止テープのようなものが非常にしっかりと貼付されているため、無理に剥がすと破損する恐れがあります。
テープが剥がれると内部基盤にアクセス可能になりますが、十分注意して作業を行ってください。
<img width="400" src="https://qiita-image-store.s3.ap-northeast-1.amazonaws.com/0/855584/965b2cdd-ce7d-455e-a39a-ec1554062551.jpeg">

テープが剥がれるとパコっと外れるので以下のように基盤部分にアクセスできます。
<img width="400" src="https://qiita-image-store.s3.ap-northeast-1.amazonaws.com/0/855584/e609542e-025e-4d8f-948e-f0b51d56d3e5.jpeg">

## 2. 基盤
以下の赤丸部分のネジをすべて外します。
<img width="400" src="https://qiita-image-store.s3.ap-northeast-1.amazonaws.com/0/855584/f0c628d1-7b01-4322-bcff-7fe14ec11cae.jpeg">

上記のネジを外すとバッテリーが外せるので下記のネジを外していきます。（画像は取ったあと）
☆部分にも隠れているので、USBモジュールを外してからネジを取っていきます。
<img width="400" src="https://qiita-image-store.s3.ap-northeast-1.amazonaws.com/0/855584/cd7ec3a4-62a0-4e23-b66c-ee2399b79118.jpeg">

## 3. バッテリーを外す
**安全のために**以下のようにバッテリーと繋がってるコネクタを外します。
ここが一番神経を使うところで、無理に外そうとするとユニット部分から線だけ抜けてしまうので慎重にやりましょう。
<img width="400" src="https://qiita-image-store.s3.ap-northeast-1.amazonaws.com/0/855584/dc458166-6634-4047-ae01-7a91adb3c31a.jpeg">

取り付けるときは表裏に気をつけて、斜め上から押し込むように接続します。

## 4.振動モジュールを取り外す
基盤の表側を向けて以下の赤丸部分の半田を取り除く、もしくは配線を切断します。
ここにつながっているのが振動する部品です。

<img width="400" src="https://qiita-image-store.s3.ap-northeast-1.amazonaws.com/0/855584/1cca05b8-ec74-4b92-ba6d-52f163d4e09e.jpeg">

取り外した部品がこれです。
こんなでかい金属が高速で振動してたらそりゃうるさい。

<img width="400" src="https://qiita-image-store.s3.ap-northeast-1.amazonaws.com/0/855584/b0c9b812-0d1d-4322-922b-618360ea5a1c.jpeg">

これで無振動化は完了です。
逆の手順で元に戻します。

# まとめ
今回、Pokémon GO Plus+ の振動を無効化するための改造手順を紹介しました。
モンボ+とは内部構造が異なり、半田作業や配線の切断が必要となる点にご注意ください。
作業は自己責任で行い、十分な注意を払って進めてください。
