using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CbeMeetingAttendance.DTOs;

namespace CbeMeetingAttendance.Services
{
    public interface IEmployeeService
    {
        Task<ApiResponse<EmployeeDto>> RegisterEmployeeAsync(RegisterEmployeeDto dto);
        Task<ApiResponse<IEnumerable<EmployeeDto>>> GetAllEmployeesAsync();
        Task<ApiResponse<EmployeeDto>> GetEmployeeByIdAsync(Guid id);
        Task<ApiResponse<EmployeeDto>> UpdateEmployeeAsync(Guid id, UpdateEmployeeDto dto);
        Task<ApiResponse<bool>> DeleteEmployeeAsync(Guid id);
        Task<ApiResponse<AttendanceCheckResultDto>> CheckEmployeeStatusAsync(string employeeId);
    }
}
