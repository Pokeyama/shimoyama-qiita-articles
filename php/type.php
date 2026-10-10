<?php
class User
{
    public string $name;
    public int $age;
    public string $health;
    public function __construct(string $name, int $age)
    {
        $this->name = $name;
        $this->age = $age;
    }
}

class Factory
{
    public static function getUserObj(string $name, int $age) // あえて返り値も書かない
    {
        $user = new User($name, $age);
        $user->health = "BAD";
        return $user;
    }

    /**
     * 連想配列版を返す例
     *
     * @param string $name
     * @param int    $age
     * @return array{name: string, age: int, health: string}
     */
    public static function getUserArr(string $name, int $age)
    {
        $arr = ["name" => $name, "age" => $age];
        $arr += ["health" => "BAD"];
        return $arr;
    }
}

$userObj = Factory::getUserObj("John", 15);
$userObj->name;
$userObj->health;

$userArr = Factory::getUserArr("John", 15);
$userArr['name'];