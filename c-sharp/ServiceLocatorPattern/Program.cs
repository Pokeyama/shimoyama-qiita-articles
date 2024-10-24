using System;

namespace ServiceLocatorPattern
{
    // サービスインターフェース
    public interface IService
    {
        string Serve();
    }

    // サービス実装
    public class ServiceA : IService
    {
        public virtual string Serve()
        {
            return "Call to non Mock.";
        }
    }

    // サービスロケーター
    public class ServiceLocator
    {
        // staticということは他のスレッドに影響しうる
        // テストのたびに実行しなおさなければならないので単純に時間がかかる
        private static IService? _service;

        // サービスを取得
        public virtual TService GetService<TService>() where TService : IService, new()
        {
            if (_service == null) _service = new TService();
            return (TService)_service;
        }
    }

    public class UseCase
    {
        ServiceLocator container;

        public void Initialize(ServiceLocator serviceLocator)
        {
            container = serviceLocator;
        }

        public string Invoke()
        {
            // このクラスで必須なServiceがこれだと不明
            // この書き方だと必ずしも必要とはいえない
            var serviceA = container.GetService<ServiceA>();  
            return serviceA.Serve();  
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            var container = new ServiceLocator();

            // サービスを取得して使用
            var service = container.GetService<ServiceA>();
            var result = service.Serve();
            Console.WriteLine(result);
        }
    }
}
