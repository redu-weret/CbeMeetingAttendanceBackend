namespace CbeMeetingAttendance.DTOs
{
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public int? Total { get; set; }
        public T? Data { get; set; }

        public static ApiResponse<T> SuccessResult(T data, string? message = null, int? total = null)
        {
            return new ApiResponse<T>
            {
                Success = true,
                Message = message,
                Total = total,
                Data = data
            };
        }

        public static ApiResponse<T> FailureResult(string message, T? data = default)
        {
            return new ApiResponse<T>
            {
                Success = false,
                Message = message,
                Data = data
            };
        }
    }
}
