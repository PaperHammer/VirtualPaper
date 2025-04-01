namespace VirtualPaper.Common.Utils {
    public class OperationResult<T> {
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public T? Result { get; set; }

        public static OperationResult<T> Success(T result) => new() { IsSuccess = true, Result = result };
        public static OperationResult<T> Failure(string errorMessage) => new() { IsSuccess = false, ErrorMessage = errorMessage };
    }
}
