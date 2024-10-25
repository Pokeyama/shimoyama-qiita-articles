using NSubstitute;
using Xunit.Abstractions;

namespace DependencyInjectionPattern.Tests;

public class UnitTest1
{
    private readonly ITestOutputHelper _output;

    public UnitTest1(ITestOutputHelper output)
    {
        _output = output;
    }


    [Fact]
    public void Client_Should_Use_Service_To_Serve()
    {
        // Arrange
        var mockService = Substitute.For<IService>();  // モックサービスの作成
        mockService.Serve().Returns("Call to Mock.");
        var dateTimeProvider = Substitute.For<DateTimeProvider>();
        dateTimeProvider.GetAccessDate().Returns(new DateTime());
        var client = new UseCase(mockService, dateTimeProvider);          // モックを注入してクライアントを作成

        // Act
        var result = client.Invoke();  // クライアントがサービスを使用
        _output.WriteLine(result);

        // Assert
        mockService.Received(1).Serve();  // モックが正しく呼び出されたか確認
    }
}