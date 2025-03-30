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
        // echo "aaa";
        // リモート画像のURL（実際のURLに置き換えてください）
        $remoteImageUrl = 'https://qiita-image-store.s3.ap-northeast-1.amazonaws.com/0/855584/4d75220b-b093-41ae-98f8-de73d4327f93.jpeg';
        $stream = fopen($remoteImageUrl, 'rb');
        if ($stream === false) {
            abort(500, "Error opening remote image");
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
