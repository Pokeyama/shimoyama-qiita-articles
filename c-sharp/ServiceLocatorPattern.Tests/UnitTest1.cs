using NSubstitute;

namespace ServiceLocatorPattern.Tests;

public class UnitTest1
{
    ServiceLocator container = Substitute.For<ServiceLocator>();

    [Fact]
    public void ServiceLocator_Should_Return_MockedService()
    {
        // Arrange
        var mockService = Substitute.For<ServiceA>();
        mockService.Serve().Returns("Call to Mock.");

        // 外からモックを渡せない
        container.GetService<ServiceA>().Returns(mockService);

        var useCase = new UseCase();
        useCase.Initialize(container);

        useCase.Invoke();

        // Assert
        mockService.Received(1).Serve();  // モックが正しく呼び出されたか確認
    }
}