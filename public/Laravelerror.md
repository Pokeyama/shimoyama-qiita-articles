---
title: 【Laravel】11.x カスタムクラスを使用したエラーハンドリング
tags:
  - PHP
  - Laravel
private: false
updated_at: ''
id: 66ecb3a021e5ff3a9bd6
organization_url_name: null
slide: false
ignorePublish: false
---
# はじめに
業務で初めてLaravelを使用することになり、コード設計をしていたのですがエラーハンドリングがうまいこといかなかったのでまとめます。
サーバー側APIでjsonを返したいだけなので`render()`のオーバーライドは考慮しません。

# 環境
Laravel 11
PHP 8.3

# 実装
いろいろ試行錯誤した結果、最終的な実装方法は以下のとおりです。
## エラーをまとめたクラス
あらかじめエラー内容を定義しておき、再利用性と保守性を高めます。
これをいい感じに使えるようにしていきます。
```php
class CustomErrors
{
    public const REGISTRATION_FAILED = [
        'code'    => 'R-001',
        'message' => 'Registration failed.',
        'status'  => 400,
    ];

    public const LOGIN_FAILED = [
        'code'    => 'L-001',
        'message' => 'Login failed.',
        'status'  => 401,
    ];
}
```

## 例外
Laravel の標準 `Exception` を拡張し、先ほど定義したエラー定数を扱いやすくします。
```php
class CustomException extends Exception
{
    protected string $errorCode;
    protected int $httpStatus;

    public function __construct(array $error, ?string $customMessage = null)
    {
        $this->errorCode = $error['code'];
        $this->httpStatus = $error['status'];
        $message = $customMessage ?: $error['message'];
        parent::__construct($message, $this->httpStatus);
    }

    public function getErrorCode(): string
    {
        return $this->errorCode;
    }

    public function getHttpStatus(): int
    {
        return $this->httpStatus;
    }

    public function getErrorMessage(): string
    {
        return $this->getMessage();
    }
}

```

## ハンドラ
例外発生時のレスポンスをJSONで返すためのハンドラークラスを作成します。
Laravel 11では、例外はグローバルに管理される `withExceptions()`経由で処理されるため、ミドルウェア内での `try-catch` ではなく、こちらで一元管理します。
```php
class ExceptionHandler
{
    public static function handle(Exceptions $exceptions): void
    {
        $exceptions->renderable(function (\Throwable $e, $request) {
            if ($e instanceof CustomException) {
                return response()->json(
                    [
                        'error_code' => $e->getErrorCode(),
                        'error'      => $e->getErrorMessage(),
                    ],
                    $e->getHttpStatus(),
                    [],
                    JSON_UNESCAPED_UNICODE
                );
            }

            // その他の例外は500エラーで返す
            return response()->json(
                [
                    'error_code' => '500',
                    'error'      => $e->getMessage(),
                ],
                500,
                [],
                JSON_UNESCAPED_UNICODE
            );
        });
    }
}
```

## 汎用例外ハンドラーに登録
`bootstrap/app.php`の`withExceptions()`に登録。
```php:bootstrap/app.php
return Application::configure(basePath: dirname(__DIR__))
    ->withRouting(
        web: __DIR__.'/../routes/web.php',
        commands: __DIR__.'/../routes/console.php',
        health: '/up',
    )
    ->withMiddleware(function (Middleware $middleware) {
    })
    ->withExceptions(function (Exceptions $exceptions) {
        // ここに追加
        ExceptionHandler::handle($exceptions);
    })->create();
```

以上で実装は終わりです。

## 疎通確認

適当にエンドポイントを作って試しておきます。

```php
Route::get('/test', function (Request $request) {
    // クエリパラメータ ?error=1 でアクセスすると例外を発生させる
    if ($request->query('error') == 1) {
        throw new CustomException(CustomErrors::REGISTRATION_FAILED);
    }
    
    if ($request->query('error') == 500) {
        // 汎用例外を発生させる（500エラー用）
        throw new Exception('Internal server error occurred.');
    }
    
    return response()->json(
        ['message' => '正常に動作しています'],
        200,
        ['Content-Type' => 'application/json'],
        JSON_UNESCAPED_UNICODE
    );
});
```

```sh
❯ curl http://localhost:8000/test
{"message":"正常に動作しています"}
❯ curl http://localhost:8000/test\?error\=1
{"error_code":"R-001","error":"Registration failed."}
❯ curl http://localhost:8000/test\?error\=500
{"error_code":"500","error":"Internal server error occurred."}
```

# ハマったポイント
## ミドルウェアでcatchできない
当初は、ミドルウェア内の `try-catch` で例外を捕捉しようとしましたが、以下のように記述してもうまく `catch`ブロックに入りませんでした。
```php
class ExceptionHandler
{
    public function handle(Request $request, Closure $next): Response
    {
        try {
            return $next($request);
        } catch (\Throwable $th) {
            // ここに入ってほしい
            if ($th instanceof ApiException) {
                return response();
            }

            return response();
        }
    }
}
```

例外は`bootstrap/app.php`の`withExceptions`に渡されてしまうそうです。
ドキュメント内だと「一元管理できるようなった」と表現されています。
https://readouble.com/laravel/11.x/ja/errors.html

## Laravel 10.x以下との違い
Laravel 10.x 以下では、例外は主に `app/Exceptions/Handler.php` やミドルウェア内の try-catch で処理されていたようです。
互換性がなさそうなのでドキュメントを読む際は気をつけたいですね。
https://readouble.com/laravel/10.x/ja/errors.html

# まとめ
今回、Laravel 11 でのカスタムクラスを使用したエラーハンドリングの実装例を紹介しました。
最初からブラウザで見やすいエラー画面が用意されていたり至れり尽くせりだなあと思ってたんですが、その分勉強しないといけないことが多そうです。
