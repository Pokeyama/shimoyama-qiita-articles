---
title: 認可サーバーを自分で作って理解する @ OpenID Connect
tags:
  - Node.js
  - TypeScript
  - openid_connect
  - Hono
private: false
updated_at: '2025-08-02T23:46:30+09:00'
id: c20e224b6f9a80177580
organization_url_name: null
slide: false
ignorePublish: false
---
# はじめに
OpenID Connectで作られた認可サーバーを自分で作って認証する側を理解しようという記事です。
サーバーはNode(Hono)、クライアントはVueで作っていきます。
**※この記事の実装は学習目的のため、認証情報の保存や鍵管理については本番環境に耐えるよう強化が必要です。**

# 事前知識
## OpenID Connect(OIDC)
こちらの記事が大変わかりやすいので一読推奨。

https://qiita.com/TakahikoKawasaki/items/498ca08bbfcc341691fe


上の記事でざっくりでも理解できたら公式ドキュメントも読んでおくと頭が良くなった気がします。

https://openid-foundation-japan.github.io/rfc6749.ja.html


個人的には一つ作っておけば色々なサービスでセキュアな状態を保ちながら、ユーザーの一意性を保てるトークンを作成できるのが特徴かなと思ってます。

![スクリーンショット 2025-08-02 23.10.40.png](https://qiita-image-store.s3.ap-northeast-1.amazonaws.com/0/855584/bfe17668-946b-41ea-b3a8-627306fe8e91.png)

# 処理フロー
以下のような流れで作成していきます。
![oidc.png](https://qiita-image-store.s3.ap-northeast-1.amazonaws.com/0/855584/324302d8-40d0-449d-b53d-f479519a5d21.png)

作成するエンドポイントは4つ

・認可リクエスト (GET /authorize)クライアント → 認証フォーム表示

・認可コード発行 (POST /authorize)認可コード発行

・認可コード交換 (POST /token)認可コード → アクセストークン・IDトークン発行

・公開鍵取得 (GET /jwks.json) → クライアントで JWT を検証

# 環境
サーバー
Node 24.3.0
Hono 4.1.4
typescript 4.0.0

クライアント
vue 3.2.0
typescript 4.0.0

キャッシュサーバー
Redis

# 実装
完全版はこちら

https://github.com/Pokeyama/minimal-oidc-auth-server-vue-client


ディレクトリ構成

```t
minimal-oidc-auth-server-vue-client/
├── server/                         # 認可サーバー実装 (Hono + Redis + TypeScript)
│   ├── src/                        
│   │   └── index.ts                # エントリポイント
│   ├── tsconfig.json               
│   └── package.json                
├── client/                         # Vue 3 クライアント実装 (Vite + TypeScript)
│   ├── src/                        
│   │   ├── views/
│   │   │   ├── Home.vue            # ログイン開始画面
│   │   │   └── Callback.vue        # 認可コード受け取り・検証画面
│   │   ├── router.ts               
│   │   └── main.ts                 # エントリポイント
│   ├── tsconfig.json               
│   └── package.json                
└── docker-compose.yml              # Redis 用コンテナ定義
```

## Redis
認可コードの管理にキャッシュを使いたいので先にDockerでRedisを準備しておきます。

```yml:docker-compose.yml
version: "3.8"

services:
  redis:
    image: redis:6-alpine
    container_name: redis
    ports:
      - "6379:6379"
```

## server
完全版はこちら。

https://github.com/Pokeyama/minimal-oidc-auth-server-vue-client/blob/main/server/src/index.ts


### RSAキー
署名検証のためのキーペアを作成してJWKの形でキャッシュしておきます。
このキーは一度生成した後、一定時間でローテーションされるように実装するのが望ましいです。
```ts
// RSA キー生成・JWK をキャッシュ
const { publicKey, privateKey } = await generateKeyPair('RS256');
const jwk = await exportJWK(publicKey);
jwk.kid = '1';
const jwks = { keys: [jwk] };
```

#### JWK の kid 指定について
`kid`は、公開鍵が複数ある場合に、どの鍵で署名されたトークンなのかを識別するためのIDです。

クライアント側は、IDトークンのヘッダーに含まれる `kid`を見て、`jwks.json`にある公開鍵の中から一致するものを探して検証を行います。
つまり、`kid`を設定しないと クライアントが正しい鍵を選べなくなる 可能性があるため、運用上は必ず設定すべき項目です。
特に鍵を定期的にローテーションする場合に重要です。

### GET /authorize
クライアントから認可を要求されたときのエントリーポイントになります。
```ts
app.get('/authorize', async c => {
  const { response_type, client_id, redirect_uri, state, nonce } = c.req.query();

  // 許可されたclient_idのみ許容
  const allowedClients = ['my-client'];
  if (!allowedClients.includes(client_id)) {
    return c.text('Unauthorized client', 401);
  }

  return c.html(`
    <form method="post" action="/authorize">
      <input name="username" placeholder="Username" />
      <input name="password" type="password" placeholder="Password" />
      <input type="hidden" name="response_type" value="${response_type}" />
      <input type="hidden" name="client_id" value="${client_id}" />
      <input type="hidden" name="redirect_uri" value="${redirect_uri}" />
      <input type="hidden" name="state" value="${state}" />
      <input type="hidden" name="nonce" value="${nonce}" />
      <button type="submit">Login</button>
    </form>
  `);
});
```

ほぼお作法ですが、クライアントから受け取るパラメータをそれぞれ解説。
`client_id`の検証を最初にしておくことで、余計な攻撃を防ぎます。

```note
response_type=code:（固定）
client_id: クライアント識別子 どのサービスから認可要求されたか判別するために使われることが多い
redirect_uri: コールバック先 URL
state: CSRF 保護用のランダム文字列
nonce: リプレイ攻撃防止用のランダム文字列
```

#### state と nonce が必要な理由
OIDC の認可コードフローでは、単に認可コードと IDトークンの署名検証だけでは不十分な脅威があります。
特に次の２つを防ぐために`state`と`nonce`が使用されます。

##### CSRF 攻撃 (state)
`state` は 認可リクエストとコールバックを結びつけるトークンで、ブラウザから `/authorize?…&state=… `を送信した後、コールバック時に同じ state が返ってくることを検証します。
これにより、リクエスト元が確かに自分のアプリケーションであることを保証し、CSRF 攻撃を防ぎます。

##### リプレイ攻撃 (nonce)
`nonce`は IDトークンに埋め込まれる一意の値で、認可サーバーが発行する JWT の`nonce`クレームと、クライアントが保持している`nonce`を照合します。
これにより、発行済みの IDトークンを第三者が再送信しても検知でき、不正利用を防止します。

### POST /authorize
`GET /authorize`で返したFormに入力されたパスワードを検証し、認可コードを発行。その後トークン取得ページにリダイレクトさせます。
パスワードは`password`で固定値にしています。実際に作るときはしっかりパスワード認証も実装しましょう。

```ts
app.post('/authorize', async c => {
  const { username, password, response_type, client_id, redirect_uri, state, nonce } = await c.req.parseBody();
  if (password !== 'password') return c.text('Unauthorized', 401);
  const code = uuidv4();
  await redis.set(
    `code:${code}`,
    JSON.stringify({ client_id, redirect_uri, state, nonce, username }),
    { EX: 300 }
  );
  return c.redirect(`${redirect_uri}?code=${code}&state=${state}`);
});
```

認可コードは有効期限付でキャッシュするのがポイント。

### POST /token
発行された認可コードを検証、アクセストークンとIDトークンを生成しクライアントに返します。

```ts
app.post('/token', async c => {
  const { grant_type, code, redirect_uri, client_id } = await c.req.json();

  if (grant_type !== 'authorization_code') return c.json({ error: 'unsupported_grant_type' }, 400);
  const raw = await redis.get(`code:${code}`);
  if (!raw) return c.json({ error: 'invalid_grant' }, 400);

  const { client_id: cid, redirect_uri: ru, state, nonce, username } = JSON.parse(raw);
  if (cid !== client_id || ru !== redirect_uri) return c.json({ error: 'invalid_grant' }, 400);

  await redis.del(`code:${code}`);

  // アクセストークンを UUID で発行
  const access_token = uuidv4();
  await redis.set(
    `token:${access_token}`,
    JSON.stringify({ username, scope: 'openid profile' }),
    { EX: 3600 }
  );

  const id_token = await new SignJWT({ sub: username, aud: client_id, nonce, name: username })
    .setProtectedHeader({ alg: 'RS256', kid: '1' })
    .setIssuedAt()
    .setIssuer('http://localhost:3000')
    .setExpirationTime('2h')
    .sign(privateKey);

  return c.json({ access_token: access_token, id_token, token_type: 'Bearer' });
});
```

#### アクセストークン
アクセストークンはAPIサーバーへの認可に使われます。
今回はUUIDで簡単に作成していますが、JWTを使って許可範囲(scope)を内包させてAPIの利用を制限させるのが一般的です。

#### IDトークン
ユーザー認証情報 を含む OIDC 固有のトークンです。
JWT フォーマットで sub (ユーザーID)、iss (発行者)、aud (クライアントID)、exp (有効期限)、nonce などのクレームを含みます。
クライアントは公開鍵 (JWKS) で署名を検証し、認証結果を信頼します。

### GET /jwks.json
IDトークンを署名検証するための公開鍵を返すAPIです。
最初にjwkでキャッシュしておいたものをそのまま返します。

```ts
app.get('/jwks.json', c => c.json(jwks));
```

以上でサーバー側の実装は終わり。

## client
デザインなどない
### Home.vue ログイン開始画面
`GET /authorize`を叩くためのページ。

```vue
<template>
  <div style="padding:2rem;">
    <h1>OIDC Client</h1>
    <button @click="login">Login with OIDC</button>
  </div>
</template>

<script lang="ts">
import { defineComponent } from 'vue';

export default defineComponent({
  setup() {
    const clientId = 'my-client';
    const redirectUri = `${window.location.origin}/callback`;
    const login = () => {
      const state = crypto.randomUUID();
      const nonce = crypto.randomUUID();
      sessionStorage.setItem('oidc_state', state);
      sessionStorage.setItem('oidc_nonce', nonce);
      const url = new URL('http://localhost:3000/authorize');
      url.searchParams.set('response_type', 'code');
      url.searchParams.set('client_id', clientId);
      url.searchParams.set('redirect_uri', redirectUri);
      url.searchParams.set('state', state);
      url.searchParams.set('nonce', nonce);
      window.location.href = url.toString();

    };
      console.log(redirectUri)

    return { login };
  }
});
</script>
```

`redirect_uri`に認可コードを受け取るページを入れるのがポイント。

### Callback.vue 認可コード受け取り・検証画面
認可コードを取得して、トークンを要求、検証して改竄をチェックします。
IDトークンの中にユーザーの名前を入れてあるので表示して終了です。

```vue
<template>
  <div style="padding:2rem;">
    <h1>Callback</h1>
    <p v-if="error">Error: {{ error }}</p>
    <p v-else-if="username">Hello, {{ username }}</p>
  </div>
</template>

<script lang="ts">
import { defineComponent, ref, onMounted } from 'vue';
import { jwtVerify, createRemoteJWKSet } from 'jose';

export default defineComponent({
  setup() {
    const username = ref<string>('');
    const error = ref<string>('');

    onMounted(async () => {
      const params = new URLSearchParams(window.location.search);
      const code = params.get('code');
      const state = params.get('state');
      if (!code || state !== sessionStorage.getItem('oidc_state')) {
        error.value = 'Invalid state';
        return;
      }
      try {
        const res = await fetch('http://localhost:3000/token', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            grant_type: 'authorization_code',
            code,
            redirect_uri: `${window.location.origin}/callback`,
            client_id: 'my-client'
          })
        });
        const tok = await res.json();
        const jwks = createRemoteJWKSet(new URL('http://localhost:3000/jwks.json'));
        const { payload } = await jwtVerify(tok.id_token, jwks, {
          issuer: 'http://localhost:3000',
          audience: 'my-client'
        });
        if (payload.nonce !== sessionStorage.getItem('oidc_nonce')) {
          throw new Error('Invalid nonce');
        }
        username.value = payload.name as string;
      } catch (e: any) {
        error.value = e.message;
      }
    });

    return { username, error };
  }
});
</script>
```

`nonce`と`state`もチェックしましょう。違いがわからなくなりますが、検証時の取得方法がそれぞれ違うのが味噌ですね。

https://qiita.com/m28/items/10c3a1de1bdcfda874b1


~~ちゃんとチェックしてる実装見たことがない~~

クライアントの実装も終わり

# 実行

```sh:server
$ cd server
$ npm install
$ npm run dev
```

```sh:client
$ cd client
$ npm install
$ npm run dev
```

```sh:redis
$ docker-compose up -d
```

以下にアクセス
http://localhost:5173/

エントリーページ
![スクリーンショット 2025-08-02 23.45.10.png](https://qiita-image-store.s3.ap-northeast-1.amazonaws.com/0/855584/b64012bd-9559-454d-a346-e21c78add5a7.png)

認証画面
![スクリーンショット 2025-08-02 22.47.40.png](https://qiita-image-store.s3.ap-northeast-1.amazonaws.com/0/855584/7b380df4-c276-4a40-8937-d63ee8810e1f.png)

先ほどの認証ページで入力したUsernameが正しく表示されれば、IDトークンの取得・検証が成功していることを意味します
![スクリーンショット 2025-08-02 22.48.07.png](https://qiita-image-store.s3.ap-northeast-1.amazonaws.com/0/855584/6174a790-2c25-434c-9a72-b986c41f0ed0.png)

あとは発行したアクセストークンをクライアントで保持しておき、APIコール時にサーバー側で検証してあげればセキュアなAPIサーバーになります。

# まとめ
色々なサービスでこの認証方法が使われていますが、逆に認証サーバー側を作ったことがなかったので簡単に実装してみました。
この記事でも色々なことをオミットしてるので、奥が深くて全て理解してる人はすごいなと改めて思いました。
