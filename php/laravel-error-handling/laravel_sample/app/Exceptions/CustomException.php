<?php

namespace App\Exceptions;

use Exception;

class CustomException extends Exception
{
    protected string $errorCode;
    protected int $httpStatus;

    /**
     * エラー情報の配列を受け取るコンストラクタ
     * 
     * @param array $error 定数で定義されたエラー情報（code, message, status）
     * @param string|null $customMessage 任意の独自メッセージ（省略時は定義されたメッセージを使用）
     */
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
