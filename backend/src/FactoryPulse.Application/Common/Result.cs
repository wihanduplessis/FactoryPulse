namespace FactoryPulse.Application.Common;

public class Result
{
    protected Result(bool isSuccess, IReadOnlyList<Error> errors)
    {
        if (isSuccess && errors.Count > 0)
            throw new InvalidOperationException("A successful result cannot contain errors.");

        if (!isSuccess && errors.Count == 0)
            throw new InvalidOperationException("A failure result must contain at least one error.");

        IsSuccess = isSuccess;
        Errors = errors;
    }

    public bool IsSuccess { get; }
    public bool IsFailure
    {
        get { return !IsSuccess; }
    }
    public IReadOnlyList<Error> Errors { get; }
    public Error FirstError
    {
        get { return IsFailure ? Errors[0] : Error.None; }
    }
        

    public static Result Success()
    {
        return new Result(true,Array.Empty<Error>());
    }
    public static Result Failure(Error error)
    {
        return new Result(false, new[] { error });
    }
    public static Result Failure(IReadOnlyList<Error> errors)
    {
        return new Result(false, errors);
    }

    public static Result<TValue> Success<TValue>(TValue value)
    {
        return new Result<TValue>(value, true, Array.Empty<Error>());
    }
    public static Result<TValue> Failure<TValue>(Error error)
    {
        return new Result<TValue>(default, false, new[] { error });
    }
    public static Result<TValue> Failure<TValue>(IReadOnlyList<Error> errors)
    {
        return new Result<TValue>(default, false, errors);
    }
}

public class Result<TValue> : Result
{
    private readonly TValue? _value;

    internal Result(TValue? value, bool isSuccess, IReadOnlyList<Error> errors)
        : base(isSuccess, errors)
    {
        _value = value;
    }

    public TValue Value
    {
        get
        {
            if (IsSuccess)
            {
                return _value!;
            }

            throw new InvalidOperationException("The value of a failure result cannot be accessed.");
        }
    }

    public static implicit operator Result<TValue>(TValue value)
    {
        return Success(value);
    }

    public static implicit operator Result<TValue>(Error error)
    {
        return Failure<TValue>(error);
    }

    public TResult Match<TResult>(
        Func<TValue, TResult> onSuccess,
        Func<IReadOnlyList<Error>, TResult> onFailure)
    {
        if (IsSuccess)
        {
            return onSuccess(Value);
        }

        return onFailure(Errors);
    }
}
