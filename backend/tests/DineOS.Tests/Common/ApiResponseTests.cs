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
    public void Ok_WithMessage_SetsMessage()
    {
        var response = ApiResponse<string>.Ok("hello", "created");
        Assert.True(response.Success);
        Assert.Equal("created", response.Message);
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

public class ApiResponseNonGenericTests
{
    [Fact]
    public void Ok_NoMessage_SetsSuccessTrue()
    {
        var response = ApiResponse.Ok();
        Assert.True(response.Success);
        Assert.Null(response.Message);
    }

    [Fact]
    public void Ok_WithMessage_SetsMessage()
    {
        var response = ApiResponse.Ok("done");
        Assert.True(response.Success);
        Assert.Equal("done", response.Message);
    }

    [Fact]
    public void Fail_SetsSuccessFalse_WithMessage()
    {
        var response = ApiResponse.Fail("oops");
        Assert.False(response.Success);
        Assert.Equal("oops", response.Message);
        Assert.Null(response.Errors);
    }

    [Fact]
    public void Fail_WithErrors_SetsErrors()
    {
        var response = ApiResponse.Fail("invalid", ["field required", "too short"]);
        Assert.False(response.Success);
        Assert.Collection(response.Errors!,
            e => Assert.Equal("field required", e),
            e => Assert.Equal("too short", e));
    }
}

public class ResultNonGenericTests
{
    [Fact]
    public void Success_SetsIsSuccessTrue_WithNullError()
    {
        var result = Result.Success();
        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Failure_SetsIsSuccessFalse_WithError()
    {
        var result = Result.Failure("something failed");
        Assert.False(result.IsSuccess);
        Assert.Equal("something failed", result.Error);
    }
}
