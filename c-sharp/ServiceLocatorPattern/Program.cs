using System;

namespace ServiceLocatorPattern
{
    // サービスインターフェース
    public interface IService
    {
        void Serve();
    }

    // サービス実装
    public class ServiceA : IService
    {
        public void Serve()
        {
            Console.WriteLine("ServiceA is serving.");
        }
    }

    // サービスロケーター
    public static class ServiceLocator
    {
        private static IService? _service;

        // サービスを登録
        public static void RegisterService(IService service)
        {
            _service = service;
        }

        // サービスを取得
        public static IService GetService()
        {
            if (_service == null) throw new InvalidOperationException("Service not registered");
            return _service;
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            // サービスロケーターにサービスを登録
            ServiceLocator.RegisterService(new ServiceA());

            // サービスを取得して使用
            var service = ServiceLocator.GetService();
            service.Serve();
        }
    }
}
