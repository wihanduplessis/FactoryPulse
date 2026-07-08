using FactoryPulse.Application.Common;

namespace FactoryPulse.Tests.Common;

public class ResultTests
{
    private static readonly Error SampleError = Error.NotFound("Test.NotFound", "Not found.");

    [Fact]
    public void Success_ShouldBeSuccessWithNoErrors()
    {
        var result = Result.Success();

        result.IsSuccess.ShouldBeTrue();
        result.IsFailure.ShouldBeFalse();
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void Failure_ShouldBeFailureWithTheError()
    {
        var result = Result.Failure(SampleError);

        result.IsFailure.ShouldBeTrue();
        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldContain(SampleError);
    }

    [Fact]
    public void Failure_WithMultipleErrors_ShouldContainAll()
    {
        var errors = new[] { Error.Validation("A", "a"), Error.Validation("B", "b") };

        var result = Result.Failure(errors);

        result.Errors.Count.ShouldBe(2);
    }

    [Fact]
    public void FirstError_OnFailure_ShouldReturnFirstError()
    {
        var result = Result.Failure(SampleError);

        result.FirstError.ShouldBe(SampleError);
    }

    [Fact]
    public void FirstError_OnSuccess_ShouldReturnNone()
    {
        var result = Result.Success();

        result.FirstError.ShouldBe(Error.None);
    }

    [Fact]
    public void GenericSuccess_ShouldExposeValue()
    {
        var result = Result.Success(42);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(42);
    }

    [Fact]
    public void GenericFailure_AccessingValue_ShouldThrow()
    {
        Result<int> result = Result.Failure<int>(SampleError);

        Should.Throw<InvalidOperationException>(() => { var value = result.Value; });
    }

    [Fact]
    public void ImplicitConversion_FromValue_ShouldCreateSuccess()
    {
        Result<int> result = 99;

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(99);
    }

    [Fact]
    public void ImplicitConversion_FromError_ShouldCreateFailure()
    {
        Result<int> result = SampleError;

        result.IsFailure.ShouldBeTrue();
        result.FirstError.ShouldBe(SampleError);
    }

    [Fact]
    public void Match_OnSuccess_ShouldRunSuccessBranch()
    {
        Result<int> result = 10;

        var output = result.Match(
            value => $"success:{value}",
            errors => $"failure:{errors.Count}");

        output.ShouldBe("success:10");
    }

    [Fact]
    public void Match_OnFailure_ShouldRunFailureBranch()
    {
        Result<int> result = SampleError;

        var output = result.Match(
            value => $"success:{value}",
            errors => $"failure:{errors.Count}");

        output.ShouldBe("failure:1");
    }
}
