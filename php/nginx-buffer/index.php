<?php
// ヘッダ設定：画像なので適切な Content-Type を送信
header('Content-Type: image/jpeg');

// もし ?remote=1 が指定されていれば、リモート画像を取得する処理に切り替え
if (isset($_GET['remote']) && $_GET['remote'] == '1') {
    // リモート画像のURL（実際のURLに置き換えてください）
    $remoteImageUrl = 'https://qiita-image-store.s3.ap-northeast-1.amazonaws.com/0/855584/4d75220b-b093-41ae-98f8-de73d4327f93.jpeg';

    // allow_url_fopen が有効であれば、fopen() で URL を直接オープンできる
    $fp = fopen($remoteImageUrl, 'rb');
    if ($fp === false) {
        http_response_code(500);
        exit("Error opening remote image");
    }
    
    // 8KBずつ読み込みながら出力
    while (!feof($fp)) {
        echo fread($fp, 8192);
        flush();
    }
    fclose($fp);
} else {
    // ローカルファイルのパス
    $imagePath = __DIR__ . '/5mb-image.jpg';

    // ファイルが存在するかチェック
    if (!file_exists($imagePath)) {
        http_response_code(404);
        exit("Image not found");
    }

    // 画像のストリームをオープン
    $fp = fopen($imagePath, 'rb');
    if ($fp === false) {
        http_response_code(500);
        exit("Error opening image");
    }

    // 8KBずつ読み込みながら出力
    while (!feof($fp)) {
        echo fread($fp, 8192);
        flush();
    }
    fclose($fp);
}
?>
