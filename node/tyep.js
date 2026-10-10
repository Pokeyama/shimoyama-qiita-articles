class User {
  health;
  constructor(name, age) {
    this.name = name;
    this.age = age;
  }
}

class Factory {
  static getUserObj(name, age) {
    const user = new User(name, age);
    user.health = "BAD";
    return user;
  }

  static getUserArr(name, age) {
    const userArr = { name: "John", age: 15 };
    userArr.health = "BAD";
    return userArr;
  }
}

const userObj = Factory.getUserObj("John", 15);
userObj.health


















const userArr = Factory.getUserArr("John", 15);
userArr.health