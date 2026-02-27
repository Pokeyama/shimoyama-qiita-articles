---
title: Spec駆動開発 4つのmdで要件→設計→実装まで
tags:
  - codex
  - sdd
  - 生成AI
  - 仕様駆動開発
private: false
updated_at: '2026-02-27T10:29:43+09:00'
id: 16a1d3c518ae5fdf8a62
organization_url_name: null
slide: false
ignorePublish: false
---
# はじめに
「AIに実装してもらう」前提で、Spec駆動開発を1本通しでやってみました。

補助ツールが色々ありますが、今回はcodexだけでやってみます。

題材は、ユーザー/管理者APIを持つ最小のガシャシステムとしました。（ほどよく実装難易度が高そうなので）
この記事では、実際に使ったファイル構成と記述例をそのまま載せます。

### 今回作ったもの
Codexに投げる仕様書

https://github.com/Pokeyama/spec-dev/tree/main/spec/gasha

吐き出した実物

https://github.com/Pokeyama/spec-dev/tree/main/apps/gasha-system

# 環境
Mac M3
Codex

# この記事でわかること
- Spec駆動開発を最小構成で始める手順
- mdファイルの役割分担
- 仕様を書いたあとに Codex へ実装依頼するときの進め方

# Spec(仕様)駆動開発とは
Spec駆動開発は、実装より先に仕様（要求・受け入れ条件・振る舞い）を定義し、その仕様を基準に設計・実装・テストを進める開発アプローチです。

AIエージェント（Codex / Claude Code）は実装手段であり、手法そのものの必須要素ではありません。
本記事では、仕様を `requirements.md` / `design.md` / `tasks.md` で固定し、`implementation.md` に検証結果を残す運用を、Spec駆動開発の実践例として扱います。

実務で回しやすい流れは次のとおりです。

1. 人間が仕様を先に固定する（要件・設計・受け入れ条件）
2. AIエージェントに実装させる（仕様を根拠に差分を作らせる）
3. 人間が「仕様との差分」でレビューする
4. 必要なら仕様に戻って更新し、再実装する
5. 完了時に `implementation.md` へ検証結果を残す

考え方自体は昔からありますが、昨今のAIで再注目された開発手法という歴史があるみたいです。

# 1. Spec開発をやってみる
今回の作業ディレクトリは以下です。

- 仕様: `spec/gasha`
- 実装: `apps/gasha-system`

※ この記事では読みやすさのため、実ディレクトリ名を簡略化して表記しています。

最初に仕様側のディレクトリで、4ファイルを順番に埋めていきました。

- `requirements.md`: 何を作るか
- `design.md`: どう作るか
- `tasks.md`: どの順で作るか
- `implementation.md`: 何が終わったか

この順で進めると、変更が出ても「どこに戻るべきか」が明確です。

# 2. 実践上の最小構成は4つのmd
「絶対にこの4ファイルでないとダメ」という意味ではありません。
ただ、AIと並走して解釈ズレを減らすには、この4つが実践上の最小セットだと思います。

| ファイル | 役割 | ここで決めること |
|---|---|---|
| `requirements.md` | 要件の固定 | Goal / Scope / FR / AC |
| `design.md` | 実装方針の固定 | API契約、DB、エラー、認証方式 |
| `tasks.md` | 実施順の固定 | 実装順、Done条件、テスト計画 |
| `implementation.md` | 実績の記録 | 実装済み項目、検証ログ、残課題 |

混同しやすいのが `tasks.md` と `implementation.md` です。

- `tasks.md`: これからやること
- `implementation.md`: 実際にやったこと

この分離で、レビューと進捗共有がかなり楽になります。

# 3. 実際に書いていく
## 3-1. 仕様書ディレクトリ構成

```text
spec/gasha
├── README.md
├── requirements.md
├── design.md
├── tasks.md
├── implementation.md
└── pokemonList.csv
```

## 3-2. requirements.md（要件）
`requirements.md` は、`技術的な事柄以外`の仕様を記載していきます。
要件定義書ですね。

全文

https://github.com/Pokeyama/spec-dev/blob/main/spec/gasha/requirements.md

以下は、実際に作成した `requirements.md` から `POST /regist` と `POST /gasha` に関係する箇所の抜粋です。

````md
## 1. Goal
単発ガシャと10連ガシャを実装する。

## 2. Problem
- アカウント作成にはIDとパスワードが必要
- アカウント毎に初期クレジット1000ダイヤが付与されている
- ガシャは1回10ダイヤ
- ガシャの景品マスタはpokemonList.csvの1列目をID、2列目をnameとしてDBに登録しておき、ランダムに排出
- アカウント毎に獲得した報酬一覧を見れるページが存在
- 管理用ページが別ドメインにあり、アカウント一覧とアカウントごとの報酬獲得状況が見れる
- jsonレスポンス

## 3. Scope
### In Scope
- `POST /regist` 入力したIDとパスワードからアカウント作成
- `POST /gasha` 単発ガシャ
- `POST /gasha/ten` 10連ガシャ

### Out of Scope
- パスワード再発行
- ダイヤ購入・課金
- 景品マスタの更新API

## 4. Functional Requirements
- FR-1: `POST /regist` は新規アカウントを作成し、初期クレジット1000ダイヤを付与し、`role=user` を設定する
- FR-2: 既に存在するIDで `POST /regist` した場合はエラーを返す
- FR-7: `POST /gasha` は10ダイヤ消費し、`pokemonList.csv` の2列目 `name` から1体をランダム排出する
- FR-8: `POST /gasha/ten` は100ダイヤ消費し、`name` から10体をランダム排出する
- FR-9: ダイヤ不足時は排出も消費も行わず、`402 Payment Required` を返す
- FR-10: ガシャで排出された景品は当該アカウントの所持一覧へ即時反映される
- FR-15: 全APIのレスポンスはJSONとする

## 5. Acceptance Criteria
1. [FR-1] 新規登録時にアカウントが作成され、残高が1000ダイヤである
2. [FR-2] 重複ID登録は `409` で失敗する
3. [FR-7, FR-10] 単発ガシャ実行で残高が10減り、報酬が1件増える
4. [FR-8, FR-10] 10連ガシャ実行で残高が100減り、報酬が10件増える
5. [FR-9] ダイヤ不足でガシャ実行した場合、残高と報酬は変化せず、`402` と `insufficient diamonds` が返る
6. [FR-13, FR-14] 管理用APIは管理者ログイン済みトークンでのみ利用でき、`account_id` を基準にアカウント一覧と個別報酬履歴が確認できる
7. [FR-15] 正常系・異常系ともに全APIレスポンスの `Content-Type` は `application/json` である
8. [NFR] 負荷走行を行い同時50アクセスでもエラー率が0.01%未満であること


````

普段から要件定義書をかっちり書いている方には耳タコですが、
ポイントは、技術名ではなく振る舞いを書くことです。

- ◯ 要件: 「何が起きるべきか」「何で失敗とみなすか」
- × 設計: 「MySQLを使う」「どのテーブルに保存する」

また、`Functional Requirements`という項目を用意し、それに対応するような`Acceptance Criteria`を記述してあげることでテスト漏れなども極力なくしていきます。

`FR`はどういう振る舞いをしてほしいか
`AC`はどういう振る舞いをしたら`FR`を満たしたと言えるか

実質テスト仕様書な内容なのでファイルを分けてもいいかもしれません。

## 3-3. design.md（設計）
`design.md` では実装方式を固定します。
実装方法や使うサービスについて具体的に書いていきます。
今回は基本設計書+詳細設計書のような形にしましたが、規模によってはファイルを分けたほうがいいと思います。

全文

https://github.com/Pokeyama/spec-dev/blob/main/spec/gasha/design.md

ここから抜粋

まず、使用言語やミドルウェア周りをバック、フロント別に記載しました。
DBやキャッシュサーバを記載する際は用途を含め書いてあげると手戻りは少なくなると思います。
```md
前提: [requirements.md](./requirements.md) の FR-1 〜 FR-15 / AC-1 〜 AC-11 を満たす。

## 1. Architecture
- 実行環境
    - Docker composeで API / MySQL / memcached を実行
    - フロントエンドは開発時にホストOS上で実行（Vue dev server）

- バックエンド
    - REST API
    - DDD/DI
    - Go
    - MySQL - master, user
    - memcached - session store
      - session:{token} -> {account_id, role, exp}

- フロントエンド
    - Vue.js　+ TypeScript + Vite
    - 開発時は `npm run dev`（HMR利用）

- 負荷テスト
    - k6
```

スキーマもここに記載。
テーブル数が多くなりそうな場合は別ファイルを用意したほうがいいように思います。
また、かけてほしい制約、逆にかけてほしくない制約も書いておきます。
今回は外部キーは使わない方針としました。
```md
## 2. Data Model
- `accounts`
  - 用途: ユーザーアカウント
  - columns:
    - `account_id` (BIGINT, PK, AUTO_INCREMENT)
    - `login_id` (VARCHAR(64), NOT NULL, UNIQUE)
    - `password_hash` (VARCHAR(255), NOT NULL)
    - `role` (ENUM('user','admin'), NOT NULL, default 'user')
    - `credit` (INT, NOT NULL, default 1000)
    - `created_at` (DATETIME, NOT NULL)
    - `updated_at` (DATETIME, NOT NULL)
  - indexes:
    - `uk_accounts_login_id (login_id)` unique
    - `idx_accounts_role (role)`

- リレーション方針
  - 今回は外部キー制約は作成しない
  - `account_id` / `reward_id` の存在確認はアプリケーションロジックで担保する
```

`requirements.md`では省略した各エンドポイントの具体的なパラメータも書いていきます。
APIごとに出してほしいエラーだったり、処理の内容、どのカラムを使ってほしいかなど極力丁寧に書いていくと手戻りが少ないです。
ここまでくると詳細設計なので実運用で1ファイルにまとめるのは良くないと思います。

```md
## 3. Endpoints Design
- 共通
  - ユーザー認証必須APIは `Authorization: Bearer <sessionToken>` を要求
  - 管理者認証必須APIは `Authorization: Bearer <adminSessionToken>` を要求
  - レスポンスは `application/json`

- `POST /regist`
  - request body: `id`, `password`
  - flow:
    - 入力値バリデーション（必須、長さ）
    - `accounts` に新規INSERT（`credit=1000`, `role='user'`）
    - unique違反は `409 ALREADY_EXISTS`
- `POST /gasha`
  - auth required
  - transaction:
    - `accounts.account_id` を `SELECT ... FOR UPDATE`
    - credit不足なら `402 INSUFFICIENT_DIAMONDS`
    - `rewards` からランダム1件選択（`reward_id`）
    - `accounts.credit = credit - 10`
    - `reward_history(account_id, reward_id, obtained_at)` に1件INSERT

## 4. API Contract
### POST /regist
Request:
{
  "id": "alice",
  "password": "pass1234"
}
Success Response: `201 Created`
{
  "id": "alice",
  "credit": 1000,
  "role": "user"
}
Duplicate ID Error: `409 Conflict`
{
  "error": {
    "code": "ALREADY_EXISTS",
    "message": "id already exists"
  }
}
### POST /gasha
Request Header:
- `Authorization: Bearer <sessionToken>`

Success Response: `200 OK`
{
  "consumedCredit": 10,
  "remainingCredit": 990,
  "rewards": [
    {
      "name": "Bulbasaur"
    }
  ]
}
Insufficient Diamonds Error: `402 Payment Required`
{
  "error": {
    "code": "INSUFFICIENT_DIAMONDS",
    "message": "insufficient diamonds"
  }
}

```

## 3-4. tasks.md（作業分解）
`tasks.md` はTODOのようなものでどういう順番で実装していくか記載し、完了時にチェックしてもらう役割になります。

全文

https://github.com/Pokeyama/spec-dev/blob/main/spec/gasha/tasks.md


抜粋

```md
## 1. Requirements / Design Fix
- [x] `requirements.md` と `design.md` の整合確認
- [x] `ui-spec.md` を追加し、UI仕様は `ui-spec.md` を正として参照する方針を明記

## 2. Project Setup
- [ ] Goプロジェクト初期化（module, ディレクトリ構成）
- [ ] Docker Composeで API / MySQL / memcached を起動可能にする
```

手書きではなく、codexに一度`requirements.md`と`design.md`を読んでもらった上で出力してもらいました。
その後、私の方でレビューを繰り返してます。

## 3-5. implementation.md（実装ログ）
`implementation.md` は「実際にやった結果の記録」と「現在の進行状況」を残す場所です。
頻繁に書き換わるものなので全文を見てもらったほうがわかりやすいかと。

https://github.com/Pokeyama/spec-dev/blob/main/spec/gasha/implementation.md

`tasks.md`と役割が被っているように見えますが、こちらはリアルタイムに今何を実装しているかを出力してもらいます。
tasksは予定であり、implementationは実績になります。


# 4. 書いたものを実装してもらう
仕様が固まったら、実装ディレクトリでAIに実装してもらいました。

```
spec/gasha/requirements.md・design.md・tasks.md・ui-spec.md を仕様の正として、apps/gasha-system に未実装分をすべて実装してください。実装後は tasks.md を更新し、implementation.md に実績と検証結果を追記してください。
```

出力された物

https://github.com/Pokeyama/spec-dev/tree/main/apps/gasha-system

## 4-1. 実装ディレクトリ構成

```text
apps/gasha-system
├── cmd/api/main.go
├── internal/
│   ├── config/
│   ├── domain/
│   ├── persistence/
│   ├── server/
│   └── session/
├── sql/
│   ├── 10_schema.sql
│   ├── 20_seed_rewards.sh
│   └── pokemonList.csv
├── frontend/
│   ├── src/
│   ├── package.json
│   └── vite.config.ts
├── perf/
│   ├── k6/
│   │   ├── user_flow.js
│   │   ├── admin_flow.js
│   │   └── load_mix.js
│   └── results/
└── docker-compose.yml
```

# 5. 手動確認
READMEも作ってもらえてたのでこれに沿って起動していきます。

https://github.com/Pokeyama/spec-dev/blob/main/apps/gasha-system/README.md

![スクリーンショット 2026-02-26 15.58.11.png](https://qiita-image-store.s3.ap-northeast-1.amazonaws.com/0/855584/24b50220-46dc-440e-bfdd-8c3b7297e7d0.png)


## 5.1 バグ
ブラウザで `POST /regist` が `Network Error` になる問題が出ました。
原因は CORS プリフライト未対応で、サーバーにログが出ない状態でした。

`design.md`に以下を追加し、修正してもらいました。
```diff
## 3. Endpoints Design
- 共通
  - ユーザー認証必須APIは `Authorization: Bearer <sessionToken>` を要求
  - 管理者認証必須APIは `Authorization: Bearer <adminSessionToken>` を要求
  - レスポンスは `application/json`
+  - CORSポリシー
+    - 許可Originは環境変数で制御（開発時は `http://127.0.0.1:5173` を許可）
+    - 許可Methodは `GET, POST, OPTIONS`
+    - ブラウザのプリフライト（`OPTIONS`）には `204 No Content` を返す
+    - 許可Headerは `Authorization, Content-Type`
```

```txt
更新したdesign.mdに基づいてコードを修正してください
```

### 5.3 完成
CORS設定後は特にバグもなく要件通りに実装されていました。
UI/UXは見ないものとする。

![スクリーンショット 2026-02-26 15.48.53.png](https://qiita-image-store.s3.ap-northeast-1.amazonaws.com/0/855584/42d0b317-f691-42a2-b578-24f6fccc9eab.png)

![スクリーンショット 2026-02-26 15.50.10.png](https://qiita-image-store.s3.ap-northeast-1.amazonaws.com/0/855584/901c94da-e79c-44c9-a70d-6220a7a05b95.png)

# 6. 初めてSpec駆動開発をやってみて
今回1行もコード書いていないんですが、正直完成したものを見て驚きました。
`design.md`にざっくり`DDD/DI`と書いていたのですが、出力されたコードもRepository層ができていてクラス設計の精度も高いかと思います。
自分自身Go初心者なのでよく見ると粗がありそうではありますが。

https://github.com/Pokeyama/spec-dev/blob/main/apps/gasha-system/internal/persistence/repository.go


反面、仕様書の精度≒実装精度となるので、コーディング工数は大幅に削減できますが、設計の工数は少し増える印象を持ちました。（というより難しい）
今まで（良くないですが）暗黙知でやってきた事象も仕様書に落とし込まなければならず、ここが今後人間の役割になっていくのかなと思いました。

# 7. まとめ
今回は好き勝手に仕様書ファイルを作ってCodexに投げましたが、この辺りを体系化してかっちりやれるツールがあるようです。しかも純国産。

https://github.com/gotalab/cc-sdd

次はこちらを使ったSpec駆動開発をやってみる予定です。

# 使用させて頂いたマスタ
https://mozukuofsea.hateblo.jp/entry/20250803/1754186286
