using System;

namespace DILifeCycle;

public interface IService
{
    Guid Serve();
}

public class ServiceA : IService
{
    public Guid InstanceId { get; private set; }

    public ServiceA()
    {
        // インスタンスIDを設定
        InstanceId = Guid.NewGuid();
        Console.WriteLine($"ServiceA instance created with ID: {InstanceId}");
    }

    public Guid Serve()
    {
        Console.WriteLine($"ServiceA is serving. Instance ID: {InstanceId}");
        return InstanceId;
    }
}