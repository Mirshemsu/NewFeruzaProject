using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeruzaShopProject.Domain.Shared
{
    public class ApiResponse<T>
    {
        public T? Data { get; set; }
        public string? ErrorMessage { get; set; }
        public string? Message { get; set; }
        public bool IsCompletedSuccessfully { get; set; }
        public DateTime GeneratedOn { get; set; } = DateTime.UtcNow;

        public static ApiResponse<T> Success(T data, string? message = null)
        {
            return new ApiResponse<T>
            {
                Data = data,
                IsCompletedSuccessfully = true,
                Message = message
            };
        }

        public static ApiResponse<T> Fail(string errorMessage)
        {
            return new ApiResponse<T>
            {
                ErrorMessage = errorMessage,
                IsCompletedSuccessfully = false
            };
        }

        public static ApiResponse<T> Fail(IEnumerable<string> errors)
        {
            return new ApiResponse<T>
            {
                ErrorMessage = string.Join(", ", errors),
                IsCompletedSuccessfully = false
            };
        }
    }
}
