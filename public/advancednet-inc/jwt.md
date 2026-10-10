---
title: JWTの先頭が「ey」じゃないトークンを作る
tags:
  - Node.js
  - C#
  - JWT
private: false
updated_at: '2026-06-22T12:09:59+09:00'
id: b45573959a34f5e9df82
organization_url_name: advancednet-inc
slide: false
ignorePublish: false
---
# はじめに

JWTって、先頭が絶対`ey`から始まるじゃないですか。

```text
eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0In0.xxxxx
```
慣れてくるとこれだけでJWTだってわかってくるじゃないですか。
それがなんとなく嫌なのでいろいろ検証してみた記事になります。

# 環境

- Mac M3
- Node.js v26 (`jsonwebtoken` 9.0.3 / `jose` 6.2.3)
- .NET 10 (JWT.NET / `JWT` 11.0.0)

# そもそもなぜ`ey`から始まるのか

JWTは`ヘッダ.ペイロード.署名`の3パートで、それぞれをbase64urlでエンコードしたものをドットでつないだ文字列です。先頭の`ey`はヘッダ部分の頭ですね。

ヘッダの中身はただのJSONで、ほぼ必ずこうなっています。

```json
{"alg":"HS256","typ":"JWT"}
```

JSONなので当然`{`、そしてキーを書くための`"`が続きます。つまり先頭2バイトは`{"`で固定。これをbase64urlにかけると`ey`になる、という感じですね。

手で追うとこうです。

- `{` = `0x7B` = `01111011`
- `"` = `0x22` = `00100010`

base64は6bitずつ区切ってエンコードするので、

- 先頭6bit `011110` = 30 → `e`
- 次の6bit `110010` = 50 → `y`

で`ey`。**JSONが`{"`で始まる限り、先頭は必ず`ey`になる**わけですね。逆に言えば、先頭の`{"`さえズラせれば`ey`は崩せそうです。

# 空白を頭に挿す

ここで思い出したいのが、JSONは構造に関係ない空白（スペース・タブ・改行）を要素の前後に入れてOKという仕様です（[RFC 8259 §2](https://www.rfc-editor.org/rfc/rfc8259#section-2) に`JSON-text = ws value ws`と定義されています）。

```json
 {"alg":"HS256","typ":"JWT"}
```

先頭にスペースを1個足しただけ。これをbase64urlにかけると先頭バイトが`{`(0x7B)から空白(0x20)に変わるので、当然`ey`じゃなくなります。

手元で各種の空白を頭に挿して、先頭2文字がどう変わるか並べてみました。

| 頭に挿すもの | バイト | 先頭2文字 |
| --- | --- | --- |
| なし | `7B` | `ey` |
| スペース | `20` | `IH` |
| タブ | `09` | `CX` |
| 改行(LF) | `0A` | `Cn` |
| 復帰(CR) | `0D` | `DX` |

`I`始まり、`C`始まり、`D`始まりが作れました。空白を2個3個と組み合わせれば2文字目以降も動くので、それっぽくバラけさせることもできます。

```text
prefix=' '    -> IHsiYWxnIj...
prefix=' \t'  -> IAl7ImFsZy...
prefix='\t\t' -> CQl7ImFsZy...
prefix='   '  -> ICAgeyJhbG...
```

で、肝心なのは**こうやって細工したトークンが、ちゃんと検証を通るのか**です。署名対象のヘッダ文字列自体を変えているので、署名から作り直さないといけません。HMACで手組みして`jsonwebtoken`で検証してみます。

```js:forge.js
const crypto = require("crypto");
const jwt = require("jsonwebtoken");
const SECRET = "test-secret";

const b64url = (buf) =>
  Buffer.from(buf).toString("base64")
    .replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");

// ヘッダJSONの先頭に prefix を挿して手組みする
function forge(prefix) {
  const header = prefix + JSON.stringify({ alg: "HS256", typ: "JWT" });
  const payload = JSON.stringify({ sub: "1234", name: "pokeyama" });
  const data = b64url(Buffer.from(header, "utf8")) + "." + b64url(Buffer.from(payload, "utf8"));
  const sig = b64url(crypto.createHmac("sha256", SECRET).update(data).digest());
  return data + "." + sig;
}

for (const [name, pre] of [["なし", ""], ["space", " "], ["tab", "\t"], ["cr", "\r"]]) {
  const token = forge(pre);
  try {
    jwt.verify(token, SECRET);
    console.log(name, token.slice(0, 2), "verify OK");
  } catch (e) {
    console.log(name, token.slice(0, 2), "verify NG", e.message);
  }
}
```

```text:結果
なし  ey verify OK
space IH verify OK
tab   CX verify OK
cr    DX verify OK
```

通りました。同じことをNodeのもう一方の定番`jose`、それとC#のJWT.NETでもやってみます。

`forge.js`と同じ要領で`jose`とJWT.NETにも食わせてみたところ、結果だけ先に書くと、空白を挿す方式は**試した全ライブラリで検証OK**でした。

| ライブラリ | space | tab | cr |
| --- | --- | --- | --- |
| jsonwebtoken (Node) | OK | OK | OK |
| jose (Node) | OK | OK | OK |
| JWT.NET (C#) | OK | OK | OK |

署名さえ作り直せば、JSONの仕様の範囲内なので当然と言えば当然ですね。

ただ、これだと`I` `C` `D`止まりで、数字始まりには届きません。base64で数字（`0`〜`9`）が出てくるのは値が52〜61のとき、つまり先頭バイトが`0xD0`〜`0xF7`あたりの非ASCIIバイトに限られます。空白（0x09〜0x20）じゃ絶対に届かない。。。

# 数字から始めたい

ここでもう一段ズルをします。**UTF-8のBOM**（`0xEF 0xBB 0xBF`）をヘッダの頭に付けます。

先頭バイトが`0xEF`になるので、

- `0xEF` = `11101111`
- 先頭6bit `111011` = 59 → `7`

base64で59番目は`7`。実際にやるとこうなります。

```text
prefix='﻿' -> 77u_eyJhbGci...
```

`77`始まり。
来ました。念願の数字始まり。
BOMは3バイト固定なので、ここから先頭を`8`や`9`に自由に変える…とまではいきませんが、とりあえず`ey`を完全に消して数字スタートにはできました。

問題はこれが検証を通るか。BOMは構造に関係ない空白とは違って、JSONの仕様上は本来「先頭に置いてはいけない」文字です。なので素直に考えると弾かれそう。各ライブラリで試してみます。

| ライブラリ | 先頭 | 検証結果 |
| --- | --- | --- |
| jsonwebtoken (Node) | `77` | NG |
| jose (Node) | `77` | **OK** |
| JWT.NET (C#) | `77` | NG |

ぬー？
同じNodeなのに`jose`は普通に通してしまうのに、`jsonwebtoken`は弾く。C#のJWT.NETも弾く。
JWT.NETは`JsonException`で、ヘッダのデコード段階でBOMを許さず落ちていました。

# なぜライブラリで結果が割れるのか

原因は、デコードしたバイト列をどうやって文字列に直してからJSONに食わせるか、という違いでした。

`jsonwebtoken`はデコードしたバイト列を`Buffer`の`toString("utf8")`で文字列化します。`Buffer`の`toString`は先頭のBOMを除去しないので、`﻿`(U+FEFF)が文字として残り、続く`JSON.parse`がこれを許しません。

```js
const bom = Buffer.from([0xef, 0xbb, 0xbf, 0x7b, 0x7d]); // BOM + "{}"
Buffer.from(bom).toString("utf8"); // "﻿{}" … 先頭に U+FEFF が残る
JSON.parse("﻿{}");                 // SyntaxError: Unexpected token '﻿'
```

一方`jose`は内部で`new TextDecoder()`を使ってバイト列を文字列に直しています。`TextDecoder`は**デフォルトで先頭のBOMを取り除く**仕様なので、`{`から始まる文字列になり、`JSON.parse`もすんなり通ります。

```js
const bom = Buffer.from([0xef, 0xbb, 0xbf, 0x7b, 0x7d]); // BOM + "{}"
new TextDecoder().decode(bom); // "{}" … BOMが消える
JSON.parse("{}");              // OK
```

C#のJWT.NETも`jsonwebtoken`と同じ側です。デコードしたバイト列を`Encoding.UTF8.GetString`で文字列化しますが、これは`Buffer.toString`と同じく先頭のBOMを残すので、続く`System.Text.Json`のパースで弾かれます。

```cs
var bom = new byte[] { 0xEF, 0xBB, 0xBF, 0x7B, 0x7D }; // BOM + "{}"
var s = Encoding.UTF8.GetString(bom); // "﻿{}" … 先頭に U+FEFF が残る
JsonDocument.Parse(s);                 // JsonException (JsonReaderException)
```


# 自前実装ならBOMトークンも通せる

`77`トークンが通るかどうかは「検証側がBOMを剥がすか」次第でした。ということは、弾く側のJWT.NETでも、自前で検証を書いてしまえば`77`始まりを通せるはずです。

ただし順番が大事で、**署名検証は受け取った`77u_...`の文字列そのままで行い、BOMを剥がすのはそのあとのJSON解釈のときだけ**にしないといけません。トークンのヘッダからBOMを抜いて`eyJ...`に戻してしまうと、署名対象の文字列が変わって署名が一致しなくなります（署名は`base64url(ヘッダ).base64url(ペイロード)`の文字列そのものにかかっているため）。

```cs:Program.cs
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using JWT.Algorithms;
using JWT.Builder;

var secret = "test-secret";

static string B64Url(byte[] b) =>
    Convert.ToBase64String(b).Replace('+', '-').Replace('/', '_').TrimEnd('=');
static byte[] B64UrlDecode(string s)
{
    s = s.Replace('-', '+').Replace('_', '/');
    s += (s.Length % 4) switch { 2 => "==", 3 => "=", _ => "" };
    return Convert.FromBase64String(s);
}

// BOM付きの 77 始まりトークンを用意する
string Forge(string prefix)
{
    var header = Encoding.UTF8.GetBytes(prefix + "{\"alg\":\"HS256\",\"typ\":\"JWT\"}");
    var payload = Encoding.UTF8.GetBytes("{\"sub\":\"1234\"}");
    var data = B64Url(header) + "." + B64Url(payload);
    using var hm = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
    return data + "." + B64Url(hm.ComputeHash(Encoding.ASCII.GetBytes(data)));
}
var token = Forge("﻿");
Console.WriteLine($"対象トークン先頭: {token[..4]}...");

// 比較用: 素のJWT.NETは弾く
try
{
    JwtBuilder.Create().WithAlgorithm(new HMACSHA256Algorithm())
        .WithSecret(secret).MustVerifySignature().Decode(token);
    Console.WriteLine("素のJWT.NET => OK");
}
catch (Exception e) { Console.WriteLine($"素のJWT.NET => NG ({e.GetType().Name})"); }

// --- BOMを許容する自前検証 ---
var parts = token.Split('.');

// 1) 署名は受け取った文字列そのままで検証する（ここを書き換えると壊れる）
using var h = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
var expected = h.ComputeHash(Encoding.ASCII.GetBytes(parts[0] + "." + parts[1]));
if (!CryptographicOperations.FixedTimeEquals(expected, B64UrlDecode(parts[2])))
    throw new Exception("invalid signature");

// 2) ヘッダはデコードしてからBOMを剥がして解釈する
var headerJson = Encoding.UTF8.GetString(B64UrlDecode(parts[0])).TrimStart('﻿');
using var doc = JsonDocument.Parse(headerJson);
var alg = doc.RootElement.GetProperty("alg").GetString();
if (alg != "HS256") throw new Exception("unexpected alg"); // algの固定は必須

Console.WriteLine($"自前検証 => OK / alg: {alg}");
```

```text:結果
対象トークン先頭: 77u_...
素のJWT.NET => NG (JsonException)
自前検証 => OK / alg: HS256
```

素のJWT.NETでは弾かれる`77`始まりトークンが、これなら検証を通りました。

とはいえ、このために署名検証を手書きするのは**全くおすすめしません。**`alg`の固定やタイミング安全な比較（`FixedTimeEquals`）など、本来ライブラリが守ってくれている部分を自分で背負うことになります。`ey`を消したいだけなら空白を挿す方式（`I`/`C`/`D`始まり）で十分だし、数字始まりのためにここまでやるのは正直割に合わないですね。

# まとめ

 `ey`で毎回始まったところでJWTの堅牢性が落ちるとかは全く無いので、用意されているライブラリできちんと検証しましょう。
