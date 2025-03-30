<?php

namespace App\Http\Controllers;

use Illuminate\Http\Request;
use Symfony\Component\HttpFoundation\StreamedResponse;

class ImageStreamController extends Controller
{
    /**
     * 画像をストリームレスポンスで返すAPI
     *
     * クエリパラメータ ?remote=1 を指定するとリモート画像、
     * それ以外の場合はローカル画像（storage/app/public/5mb-image.jpg）をストリームします。
     */
    public function streamImage(Request $request): StreamedResponse
    {
        if ($request->query('remote') == '1') {
            // リモート画像のURL（実際のURLに置き換えてください）
            $remoteImageUrl = 'https://example.com/path/to/5mb-image.jpg';
            $stream = fopen($remoteImageUrl, 'rb');
            if ($stream === false) {
                abort(500, "Error opening remote image");
            }
        } else {
            // ローカル画像のパス（storage/app/public/5mb-image.jpg に配置してください）
            $localPath = storage_path('app/public/5mb-image.jpg');
            if (!file_exists($localPath)) {
                abort(404, "Local image not found");
            }
            $stream = fopen($localPath, 'rb');
            if ($stream === false) {
                abort(500, "Error opening local image");
            }
        }

        return response()->stream(function () use ($stream) {
            // 8KBずつ読み込みながら出力
            while (!feof($stream)) {
                echo fread($stream, 8192);
                flush();
            }
            fclose($stream);
        }, 200, [
            'Content-Type' => 'image/jpeg'
        ]);
    }
}
