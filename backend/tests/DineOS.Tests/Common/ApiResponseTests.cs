using DineOS.Application.Common;

namespace DineOS.Tests.Common;

public class ApiResponseTests
{
    [Fact]
    public void Ok_SetsSuccessTrue()
    {
        var response = ApiResponse<string>.Ok("hello");
        Assert.True(response.Success);
        Assert.Equal("hello", response.Data);
        Assert.Null(response.Errors);
    }

    [Fact]
    public void Fail_SetsSuccessFalse()
    {
        var response = ApiResponse<string>.Fail("something went wrong", ["err1"]);
        Assert.False(response.Success);
        Assert.Null(response.Data);
        Assert.Contains("err1", response.Errors!);
    }

    [Fact]
    public void Result_Success_IsSuccess()
    {
        var result = Result<int>.Success(42);
        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Result_Failure_HasError()
    {
        var result = Result<int>.Failure("not found");
        Assert.False(result.IsSuccess);
        Assert.Equal("not found", result.Error);
    }
}
