<?php

namespace App\Exceptions;

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
