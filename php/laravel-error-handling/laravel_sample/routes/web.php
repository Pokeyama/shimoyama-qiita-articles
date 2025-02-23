<?php

use Illuminate\Support\Facades\Route;
use Illuminate\Http\Request;
use App\Exceptions\CustomException;
use App\Exceptions\CustomErrors;

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
