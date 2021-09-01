using System.Diagnostics.CodeAnalysis;

namespace BlazeBin.Shared
{
    public class Result<T> where T : class
    {
        public T? Value { get; private set; }

        [MemberNotNullWhen(true, nameof(Value))]
        [MemberNotNullWhen(false, nameof(Error))]
        public bool Successful { get; private set; }

        public string? Error { get; private set; }

        protected Result(T result)
        {
            Value = result;
            Successful = true;
        }

        protected Result(string error)
        {
            Successful = false;
            Error = error;
        }

        public static Result<T> FromSuccess(T result)
        {
            return new Result<T>(result);
        }

        public static Result<T> FromError(string error)
        {
            return new Result<T>(error);
        }
    }
}
