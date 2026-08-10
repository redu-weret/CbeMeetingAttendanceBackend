using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CbeMeetingAttendance.DTOs;
using CbeMeetingAttendance.Enums;
using CbeMeetingAttendance.Mappers;
using CbeMeetingAttendance.Models;
using CbeMeetingAttendance.Repositories;

namespace CbeMeetingAttendance.Services
{
    public class EmployeeService : IEmployeeService
    {
        private readonly IEmployeeRepository _employeeRepository;
        private readonly IAttendanceRepository _attendanceRepository;
        private readonly IUserRepository _userRepository;

        public EmployeeService(IEmployeeRepository employeeRepository, IAttendanceRepository attendanceRepository, IUserRepository userRepository)
        {
            _employeeRepository = employeeRepository ?? throw new ArgumentNullException(nameof(employeeRepository));
            _attendanceRepository = attendanceRepository ?? throw new ArgumentNullException(nameof(attendanceRepository));
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        }

        public async Task<ApiResponse<EmployeeDto>> RegisterEmployeeAsync(RegisterEmployeeDto dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));

            var exists = await _employeeRepository.ExistsAsync(dto.EmployeeId);
            if (exists)
            {
                return ApiResponse<EmployeeDto>.FailureResult($"Employee with ID '{dto.EmployeeId}' is already registered.");
            }

            var employee = dto.ToEntity();
            await _employeeRepository.AddAsync(employee);

            return ApiResponse<EmployeeDto>.SuccessResult(employee.ToDto(), "Employee registered successfully.");
        }

        public async Task<ApiResponse<IEnumerable<EmployeeDto>>> GetAllEmployeesAsync()
        {
            var employees = await _employeeRepository.GetAllAsync();
            var paths = await _userRepository.GetProfilePicturePathsByEmployeeIdsAsync(employees.Select(e => e.Id));

            var dtos = employees.Select(e =>
            {
                var dto = e.ToDto();
                dto.ProfilePicturePath = paths.TryGetValue(e.Id, out var path) ? path : null;
                return dto;
            });

            return ApiResponse<IEnumerable<EmployeeDto>>.SuccessResult(dtos, "Employees retrieved successfully.");
        }

        public async Task<ApiResponse<EmployeeDto>> GetEmployeeByIdAsync(Guid id)
        {
            var employee = await _employeeRepository.GetByIdAsync(id);
            if (employee == null)
            {
                return ApiResponse<EmployeeDto>.FailureResult("Employee not found.");
            }

            var dto = employee.ToDto();
            var paths = await _userRepository.GetProfilePicturePathsByEmployeeIdsAsync(new[] { employee.Id });
            dto.ProfilePicturePath = paths.TryGetValue(employee.Id, out var path) ? path : null;

            return ApiResponse<EmployeeDto>.SuccessResult(dto, "Employee retrieved successfully.");
        }

        public async Task<ApiResponse<EmployeeDto>> UpdateEmployeeAsync(Guid id, UpdateEmployeeDto dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));

            var employee = await _employeeRepository.GetByIdAsync(id);
            if (employee == null)
            {
                return ApiResponse<EmployeeDto>.FailureResult("Employee not found.");
            }

            // Update properties
            employee.FullName = dto.FullName.Trim();
            employee.Division = dto.Division.Trim();
            employee.JobTitle = dto.JobTitle.Trim();
            employee.Department = dto.Department.Trim();
            employee.PhoneNumber = dto.PhoneNumber.Trim();

            await _employeeRepository.UpdateAsync(employee);

            return ApiResponse<EmployeeDto>.SuccessResult(employee.ToDto(), "Employee updated successfully.");
        }

        public async Task<ApiResponse<bool>> DeleteEmployeeAsync(Guid id)
        {
            var employee = await _employeeRepository.GetByIdAsync(id);
            if (employee == null)
            {
                return ApiResponse<bool>.FailureResult("Employee not found.");
            }

            // Prevent deleting employees that have a linked user account to avoid FK constraint violations
            if (await _userRepository.ExistsByEmployeeIdAsync(employee.Id))
            {
                return ApiResponse<bool>.FailureResult("Cannot delete employee because a user account is linked to this employee. Remove or reassign the user first.");
            }

            await _employeeRepository.DeleteAsync(employee);
            return ApiResponse<bool>.SuccessResult(true, "Employee deleted successfully.");
        }

        public async Task<ApiResponse<AttendanceCheckResultDto>> CheckEmployeeStatusAsync(string employeeId)
        {
            if (string.IsNullOrWhiteSpace(employeeId))
            {
                return ApiResponse<AttendanceCheckResultDto>.FailureResult("Employee ID is required.");
            }

            var employee = await _employeeRepository.GetByEmployeeIdAsync(employeeId);
            if (employee == null)
            {
                return ApiResponse<AttendanceCheckResultDto>.FailureResult($"Employee with ID '{employeeId}' not found in the system.");
            }

            var today = DateOnly.FromDateTime(DateTime.Now);
            var currentHour = DateTime.Now.Hour;
            var session = currentHour < 12 ? Session.Morning : Session.Afternoon;

            var attendance = await _attendanceRepository.GetAttendanceAsync(employee.Id, today, session);
            var result = employee.ToCheckResultDto(today, session, attendance);

            string statusMsg = result.IsAttended
                ? "Employee has attended this session."
                : "Employee has not attended this session.";
            return ApiResponse<AttendanceCheckResultDto>.SuccessResult(result, statusMsg);
        }
    }
}
