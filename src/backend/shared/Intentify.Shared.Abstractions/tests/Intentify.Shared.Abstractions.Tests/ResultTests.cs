using Intentify.Shared.Abstractions;

namespace Intentify.Shared.Abstractions.Tests;

public class ResultTests
{
    [Fact]
    public void Result_Success_HasSuccessTrueAndErrorNull()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Result_Failure_HasSuccessFalseAndErrorSet()
    {
        var error = new Error("ERR", "Something failed");
        var result = Result.Failure(error);

        Assert.False(result.IsSuccess);
        Assert.Equal(error, result.Error);
    }

    [Fact]
    public void GenericResult_Success_ReturnsExpectedValue()
    {
        const string value = "ok";
        var result = Result<string>.Success(value);

        Assert.True(result.IsSuccess);
        Assert.Equal(value, result.Value);
        Assert.Null(result.Error);
    }

    [Fact]
    public void GenericResult_Failure_HasNoValueAndErrorSet()
    {
        var error = new Error("ERR", "Something failed");
        var result = Result<string>.Failure(error);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Equal(error, result.Error);
    }
}
