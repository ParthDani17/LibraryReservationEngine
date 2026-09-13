namespace LibraryReservationEngine.Application.Common
{
    // Simple success/failure wrapper so services don't need to throw exceptions
    // for expected outcomes (e.g. "no copy available").
    public class Result
    {
        public bool Success { get; }
        public string? Message { get; }

        protected Result(bool success, string? message)
        {
            Success = success;
            Message = message;
        }

        public static Result Ok(string? message = null) => new Result(true, message);
        public static Result Fail(string message) => new Result(false, message);
    }
}
