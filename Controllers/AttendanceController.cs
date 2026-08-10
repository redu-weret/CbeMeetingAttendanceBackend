using System;
using System.Security.Claims;
using System.Threading.Tasks;
using CbeMeetingAttendance.DTOs;
using CbeMeetingAttendance.Repositories;
using CbeMeetingAttendance.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CbeMeetingAttendance.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api")]
    public class AttendanceController : ControllerBase
    {
        private readonly IAttendanceService _attendanceService;
        private readonly IEmployeeRepository _employeeRepository;

        public AttendanceController(IAttendanceService attendanceService, IEmployeeRepository employeeRepository)
        {
            _attendanceService = attendanceService ?? throw new ArgumentNullException(nameof(attendanceService));
            _employeeRepository = employeeRepository ?? throw new ArgumentNullException(nameof(employeeRepository));
        }

        // Check Attendance: GET /api/attendance/check
        // All authenticated users (including non-admins) can check attendance
        [HttpGet("attendance/check")]
        public async Task<ActionResult<ApiResponse<AttendanceCheckResultDto>>> Check(
            [FromQuery] string employeeId,
            [FromQuery] string? date = null,
            [FromQuery] string? session = null)
        {
            var result = await _attendanceService.CheckAttendanceAsync(employeeId, date, session);
            if (!result.Success)
            {
                return result.Message?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true
                    ? NotFound(result)
                    : BadRequest(result);
            }
            return Ok(result);
        }

        // Take Attendance: POST /api/attendance
        // Attendance is always recorded for the currently logged-in user
        [HttpPost("attendance")]
        public async Task<ActionResult<ApiResponse<AttendanceDto>>> Take([FromBody] TakeAttendanceDto dto)
        {
            // Extract the logged-in user's EmployeeId (GUID) from the JWT claim
            var employeeIdClaim = User.FindFirstValue("EmployeeId");
            if (string.IsNullOrWhiteSpace(employeeIdClaim) || !Guid.TryParse(employeeIdClaim, out var employeeGuid))
            {
                return Unauthorized(ApiResponse<AttendanceDto>.FailureResult("Your account is not linked to an employee record."));
            }

            // Look up the employee by GUID to get their string EmployeeId (e.g. "EMP001")
            var employee = await _employeeRepository.GetByIdAsync(employeeGuid);
            if (employee == null)
            {
                return BadRequest(ApiResponse<AttendanceDto>.FailureResult("Your linked employee record was not found."));
            }

            // Override the dto.EmployeeId with the current user's employee ID
            dto.EmployeeId = employee.EmployeeId;

            var result = await _attendanceService.TakeAttendanceAsync(dto);
            if (!result.Success)
            {
                return BadRequest(result);
            }
            return Ok(result);
        }

        // Reports: GET /api/admin/dashboard
        [Authorize(Roles = "Admin")]
        [HttpGet("admin/dashboard")]
        public async Task<ActionResult<ApiResponse<DashboardDto>>> GetDashboard()
        {
            var result = await _attendanceService.GetDashboardStatsAsync();
            return Ok(result);
        }

        // Export Reports: GET /api/admin/export/excel?format=excel|csv|pdf
        [Authorize(Roles = "Admin")]
        [HttpGet("admin/export/excel")]
        public async Task<IActionResult> ExportExcel([FromQuery] string? format = "excel")
        {
            var resolvedFormat = string.IsNullOrWhiteSpace(format) ? "excel" : format;
            var export = await _attendanceService.ExportAttendanceAsync(resolvedFormat);
            return File(export.FileBytes, export.ContentType, export.FileName);
        }
    }
}
