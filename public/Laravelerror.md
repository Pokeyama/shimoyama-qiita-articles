---
title: 【Laravel】11.x カスタムクラスを使用したエラーハンドリング
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
業務で初めてLaravelを使用することになり、コード設計をしていたのですがエラーハンドリングがうまいこといかなかったのでまとめます。
サーバー側APIでjsonを返したいだけなので`render()`のオーバーライドは考慮しません。

# 環境
Laravel 11
PHP 8.3

# 実装
いろいろ試したんですが、最初に実装方法を。
## エラーをまとめたクラス
以下のようにエラーをあらかじめ定義しておきます。
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
`Exception`を拡張して先ほど定義したエラークラスを扱いやすくしておきます。
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

## Hndler
ミドルウェアに挿入するようのハンドラーを作ります。
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
        //
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

## Laravel 10.x以下との違い


