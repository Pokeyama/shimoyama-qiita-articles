<?php

header('Content-Type: application/json');

// New Relicでトランザクション名を設定
if (extension_loaded('newrelic')) {
    $request_uri = parse_url($_SERVER['REQUEST_URI'], PHP_URL_PATH);
    newrelic_name_transaction($request_uri);
}

// ルーティング処理
$request = parse_url($_SERVER['REQUEST_URI'], PHP_URL_PATH);

switch ($request) {
    case '/auth':
        authEndpoint();
        break;
    case '/login':
        loginEndpoint();
        break;
    case '/info':
        infoEndpoint();
        break;
    case '/start':
        startEndpoint();
        break;
    case '/result':
        resultEndpoint();
        break;
    default:
        http_response_code(404);
        echo json_encode(["message" => "Endpoint not found"]);
        break;
}

// 各エンドポイント
function authEndpoint() {
    echo json_encode(["message" => "Auth endpoint"]);
}

function loginEndpoint() {
    sleep(1);
    echo json_encode(["message" => "Login endpoint"]);
}

function infoEndpoint() {
    echo json_encode(["message" => "Info endpoint"]);
}

function startEndpoint() {
    echo json_encode(["message" => "Start endpoint"]);
}

function resultEndpoint() {
    echo json_encode(["message" => "Result endpoint"]);
}