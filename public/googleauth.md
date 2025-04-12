---
title: GoogleのOAuth認証でアクセストークンを取得する
tags:
  - 'googleapi'
  - 'Node.js'
  - 'Vue.js'
  - 'OAuth2.0'
private: false
updated_at: ''
id: null
organization_url_name: null
slide: false
ignorePublish: false
---
# はじめに

Google 関連の API を利用する際、OAuth2.0 による認証を通じて取得できるアクセストークンが必要になります。  
サーバー側開発者なら、以下の公式ドキュメントは穴が空くほど目を通しているのではないでしょうか。

https://developers.google.com/identity/protocols/oauth2?hl=ja

もう読みたくないので、Qiitaにまとめておきます。
勉強のためにNodeで書いてますが、実装方法自体は他言語でも同じです。

---

<details><summary>(余談)従来の実装</summary>
これまで、認可コードを取得するために、

- ブラウザのアドレスバーに直接エンドポイントを入力して認可コードを受け取る

という手作業の方法を使っていました。
その後、

1. 認可コードを受け取る

1. サーバー側でアクセストークンを取得する

1. 取得したアクセストークンを DB やキャッシュに保存する

という一連の処理を手作業で行っていました。
従来、私自身もこれらの手順を実装していましたが、毎回面倒に感じていました。  

### サービスアカウントの利用
サービスアカウントを利用する方法も存在しますが、
これは主にバックグラウンド処理など特定の用途向けの仕組みとして利用されるものであり、従来の手動で認可コードを取得する方法とは性格が異なります。

### 新たな実装拝見
しかし、今回、認可コードの取得からアクセストークンの取得までを一貫してコード内で完結させる実装例を見かけたため、
私もこの手法を試してみることにしました。


</details>

---

# 環境

- **OS:** Mac M3 (Sequoia)
- **サーバー:** Node.js / Express
- **フロントエンド:** Vue.js

# フロー

大まかな処理の流れは以下の通りです。

1. 認可コードの取得  
2. アクセストークンの取得  
3. 取得したアクセストークンを用いてユーザー情報を取得（アクセストークンが有効か確認するため）  
4. （オプション）ログアウト処理

以下のシーケンス図に則って実装していきます。

![フロー図](https://qiita-image-store.s3.ap-northeast-1.amazonaws.com/0/855584/9ccb4a15-3df9-451f-92c6-2c5e6528cc6b.jpeg)

作成する API は次の 4 つです。

- `/auth/google`  
  → ユーザーを Google 認証ページにリダイレクトする

- `/auth/google/callback`  
  → Google 認可サーバーから認可コードを受け取り、アクセストークンを取得してキャッシュする

- `/profile`  
  → キャッシュしたアクセストークンを用いてユーザー情報を取得する

- `/logout`  
  → キャッシュしていた情報をクリアする

# 認証情報作成
[GCPコンソール](https://console.cloud.google.com/)から「APIとサービス」→「認証情報」と下りていき、「認証情報の作成」
「OAuthクライアントID」をクリック

![スクリーンショット 2025-04-12 17.02.27.png](https://qiita-image-store.s3.ap-northeast-1.amazonaws.com/0/855584/bb4feb83-be8f-4f35-bcbe-d3c3e0573b92.png)


アプリケーションの種類は「ウェブアプリケーション」
**(重要)承認済みのリダイレクト URIは 「サーバー側APIのエンドポイント」 を書いておきます。**

![スクリーンショット 2025-04-12 17.05.18.png](https://qiita-image-store.s3.ap-northeast-1.amazonaws.com/0/855584/cf9028ae-bf32-4d1b-8688-4affb61ec541.png)

作成が終わったら各種認証情報を保管しておきます。

## リダイレクト URI をサーバー側 API にする理由
リダイレクト URI を自社の Web サイトに設定して、自分でブラウザから認可コードを取得するケースも見受けられますが、  
API サーバーのエンドポイントにリダイレクトさせることで、サーバー側で認可コード付きの URI を直接受け取れます。

たとえば、下記のように実装できます。

```ts
app.get(
  '/auth/google/callback',
  async (req: Request, res: Response, next: NextFunction): Promise<void> => {
    // 認可コード取得
    const code = req.query.code as string;
    // 続く処理・・・
  }
)
```

この発想が今までなかった。悲しい。

# 実装
完全な実装は以下のレポジトリを御覧ください。

https://github.com/Pokeyama/google-oath2/tree/main

## サーバー
CORSなどは省略します。

### cookie
今回はcookie-session を利用してセッション管理を行います。
```ts:index.ts
app.use(
  cookieSession({
    name: 'session',
    keys: ['your-secret-key'],
    maxAge: 24 * 60 * 60 * 1000, // 24時間
  })
);
```

### Google OAuthクライアント
Google OAuth2.0 クライアントは使い回すため、アプリケーション起動時に一度だけ作成します。
都度生成はしないように気をつけましょう。
```ts:index.ts
// Google OAuth2.0 クライアントの設定
const CLIENT_ID = process.env.CLIENT_ID || 'YOUR_GOOGLE_CLIENT_ID';
const CLIENT_SECRET = process.env.CLIENT_SECRET || 'YOUR_GOOGLE_CLIENT_SECRET';
const REDIRECT_URI = 'http://localhost:3000/auth/google/callback'; // Google Cloud Console と合わせる

const oauth2Client = new google.auth.OAuth2(
  CLIENT_ID,
  CLIENT_SECRET,
  REDIRECT_URI
);
```

### ユーザーに認証させる
利用する API のスコープを指定し、ユーザーを認証ページにリダイレクトさせます。
`prompt: 'consent'` としておくと、アクセストークンが既に存在していても再度同意画面が表示され、新しいトークンを取得できます。

```ts:index.ts
// ユーザ情報取得のスコープ設定
const scopes = [
  'https://www.googleapis.com/auth/userinfo.profile',
  'https://www.googleapis.com/auth/userinfo.email'
];

// 認証フロー開始エンドポイント
app.get(
  '/auth/google',
  (req: Request, res: Response, next: NextFunction): void => {
    try {
      const authUrl = oauth2Client.generateAuthUrl({
        access_type: 'offline',
        scope: scopes,
        prompt: 'consent'
      });
      res.redirect(authUrl);
    } catch (error) {
      next(error);
    }
  }
);
```

### アクセストークン取得
Google 認可サーバーからリダイレクトされてくる際に認可コードを受け取り、アクセストークンを取得します。
ここでは、後で `/profile` でユーザー情報を利用できるよう、アクセストークンをセッションにキャッシュします。

```ts:index.ts
// OAuth2 コールバックエンドポイント
app.get(
  '/auth/google/callback',
  async (req: Request, res: Response, next: NextFunction): Promise<void> => {
    const code = req.query.code as string;
    if (!code) {
      res.status(400).send('認証コードがありません');
      return;
    }
    try {
      // 認証コードからアクセストークンを取得
      const { tokens } = await oauth2Client.getToken(code);
      oauth2Client.setCredentials(tokens);

      // セッションにトークンを保存
      req.session!.tokens = tokens;
      
      // フロントエンド（Vite サーバー）にリダイレクト
      res.redirect('http://localhost:5173');
    } catch (error) {
      console.error('アクセストークン取得エラー:', error);
      next(error);
    }
  }
);
```

### ユーザー情報取得
キャッシュしたアクセストークンを使用してユーザー情報を取得します。
```ts:index.ts
// ログインユーザーのプロフィールを取得するためのエンドポイント
app.get(
  '/profile',
  async (req: Request, res: Response, next: NextFunction): Promise<void> => {
    if (!req.session) {
      res.status(401).send('未認証です');
      return;
    }
    
    // ユーザー情報を取得
    const oauth2 = google.oauth2({
      auth: oauth2Client,
      version: 'v2',
    });
    const userInfoResponse = await oauth2.userinfo.get();
    const user = userInfoResponse.data;

    res.json(user);
  }
);
```

### ログアウト
キャッシュしている情報をクリアするだけです。
```ts:index.ts
// ログアウトエンドポイント：セッションをクリアしてフロントにリダイレクト
app.get(
  '/logout',
  (req: Request, res: Response, next: NextFunction): void => {
    req.session = null;
    res.redirect('http://localhost:5173');
  }
);
```

## フロント
サーバー側のAPIに合わせて画面を構成するだけです。
勉強のためにVue.jsを使用します。
CSSは省略。

### テンプレート
`user`オブジェクトに値が無い場合はログインを促し、値がある場合はプロフィール情報とログアウトボタンを表示します。
```vue
<template>
  <div class="container">
    <h1>Google OAuth2.0 認証デモ</h1>

    <!-- 未ログイン時 -->
    <div v-if="!user">
      <button @click="loginWithGoogle">Googleでログイン</button>
    </div>
    
    <!-- ログイン済み時 -->
    <div v-else>
      <h2>プロフィール情報</h2>
      <p><strong>名前：</strong>{{ user.name }}</p>
      <p><strong>メール：</strong>{{ user.email }}</p>
      <!-- 画像をブロック要素にして中央寄せ -->
      <img :src="user.picture" alt="ユーザー画像" />
      <!-- ログアウトボタンもブロック要素にし、改行して中央寄せ -->
      <button @click="logout" class="logout">ログアウト</button>
    </div>
  </div>
</template>
```

![スクリーンショット 2025-04-12 18.29.21.png](https://qiita-image-store.s3.ap-northeast-1.amazonaws.com/0/855584/06ea306f-f88e-4d09-80c0-2a1554f05be4.png)

### APIをコールする
ページ遷移を避けるため、`/profile` エンドポイントは Axios を用いて非同期で呼び出します。
ここではクロスオリジン設定のため `withCredentials: true` を明示しています。
```vue:App.vue
<script lang="ts">
import { defineComponent, ref, onMounted } from 'vue';
import axios from 'axios';

export default defineComponent({
  name: 'App',
  setup() {
    const user = ref<any>(null);

    // Google認証開始
    const loginWithGoogle = () => {
      window.location.href = 'http://localhost:3000/auth/google';
    };

    // プロフィール取得
    const fetchUserProfile = async () => {
      try {
        const res = await axios.get('http://localhost:3000/profile', {
          withCredentials: true
        });
        user.value = res.data;
      } catch (error) {
        console.error('プロフィール取得エラー:', error);
      }
    };

    // ログアウト
    const logout = () => {
      window.location.href = 'http://localhost:3000/logout';
    };

    // ページ読み込み時にプロフィール取得
    onMounted(() => {
      fetchUserProfile();
    });

    return {
      user,
      loginWithGoogle,
      logout,
    };
  },
});
</script>
```

# 完成
実行して画面遷移を見ていきます。
```sh
# clinet
$ npm run dev

> client@0.0.0 dev
> vite


  VITE v6.2.5  ready in 203 ms

  ➜  Local:   http://localhost:5173/
  ➜  Network: use --host to expose
  ➜  press h + enter to show help

#server
$ npx ts-node index.ts
サーバーがポート 3000 で起動しました
```

トップページ
![スクリーンショット 2025-04-12 18.29.21.png](https://qiita-image-store.s3.ap-northeast-1.amazonaws.com/0/855584/06ea306f-f88e-4d09-80c0-2a1554f05be4.png)

ログインボタンクリック後、Google認証ページに遷移
![スクリーンショット 2025-04-12 18.44.02.png](https://qiita-image-store.s3.ap-northeast-1.amazonaws.com/0/855584/8a519bcb-eb16-46c1-81b8-654db0957544.png)

![スクリーンショット 2025-04-12 18.46.19.png](https://qiita-image-store.s3.ap-northeast-1.amazonaws.com/0/855584/cf11bdd6-ac92-479b-9ace-01ceef8f64e6.png)

トップページにリダイレクトされるので、`/profile`が叩かれてユーザー情報を表示します。
![スクリーンショット 2025-04-12 18.53.53.png](https://qiita-image-store.s3.ap-northeast-1.amazonaws.com/0/855584/89fe7e9d-b467-497e-8495-04b9a473a983.png)

# まとめ
この記事では、ブラウザのアドレスバーに直接エンドポイントを入力して認可コードを取得していた手作業の認証フローを、コード内で一貫して自動化する手法を紹介しました。
もっと頭柔らかくしないとだめですね。