<?php

namespace App\Http\Middleware;

use Closure;
use Illuminate\Http\Request;
use Symfony\Component\HttpFoundation\Response;
use Illuminate\Foundation\Configuration\Exceptions; // 正しい Exceptions クラスをインポート
use App\Exceptions\CustomException; // カスタム例外の名前空間

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
