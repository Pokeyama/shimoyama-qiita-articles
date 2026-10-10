<?php

use Illuminate\Support\Facades\Route;

Route::get('/', function () {
    echo "aaa";
    // return view('welcome');
});

use App\Http\Controllers\ImageStreamController;

Route::get('/stream-image', [ImageStreamController::class, 'streamImage']);
