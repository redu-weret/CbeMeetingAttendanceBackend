using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CbeMeetingAttendance.DTOs;
using CbeMeetingAttendance.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CbeMeetingAttendance.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api")]
    public class EmployeeController : ControllerBase
    {
        private readonly IEmployeeService _employeeService;

        public EmployeeController(IEmployeeService employeeService)
        {
            _employeeService = employeeService ?? throw new ArgumentNullException(nameof(employeeService));
        }

        // Registration: POST /api/employees/register
        [Authorize(Roles = "Admin")]
        [HttpPost("employees/register")]
        public async Task<ActionResult<ApiResponse<EmployeeDto>>> Register([FromBody] RegisterEmployeeDto dto)
        {
            var result = await _employeeService.RegisterEmployeeAsync(dto);
            if (!result.Success)
            {
                return BadRequest(result);
            }
            return Ok(result);
        }

        // Get All Employees: GET /api/employees
        [Authorize(Roles = "Admin")]
        [HttpGet("employees")]
        public async Task<ActionResult<ApiResponse<IEnumerable<EmployeeDto>>>> GetAll()
        {
            var result = await _employeeService.GetAllEmployeesAsync();
            return Ok(result);
        }

        // Get Employee By Id (Guid): GET /api/employee/id
        [Authorize(Roles = "Admin")]
        [HttpGet("employee/{id:guid}")]
        public async Task<ActionResult<ApiResponse<EmployeeDto>>> GetById(Guid id)
        {
            var result = await _employeeService.GetEmployeeByIdAsync(id);
            if (!result.Success)
            {
                return NotFound(result);
            }
            return Ok(result);
        }

        // Update Employee Data: PUT /api/employee/id
        [Authorize(Roles = "Admin")]
        [HttpPut("employee/{id:guid}")]
        public async Task<ActionResult<ApiResponse<EmployeeDto>>> Update(Guid id, [FromBody] UpdateEmployeeDto dto)
        {
            var result = await _employeeService.UpdateEmployeeAsync(id, dto);
            if (!result.Success)
            {
                return BadRequest(result);
            }
            return Ok(result);
        }

        // Delete an Employee: DELETE /api/employee/id
        [Authorize(Roles = "Admin")]
        [HttpDelete("employee/{id:guid}")]
        public async Task<ActionResult<ApiResponse<bool>>> Delete(Guid id)
        {
            var result = await _employeeService.DeleteEmployeeAsync(id);
            if (!result.Success)
            {
                if (!string.IsNullOrWhiteSpace(result.Message) && result.Message.Contains("user account", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(result);
                }
                return NotFound(result);
            }
            return Ok(result);
        }

        // Check Employee by employeeId string (e.g. EMP001): GET /api/employees/{employeeId}
        // All authenticated users can check employee existence for taking attendance
        [HttpGet("employees/{employeeId}")]
        public async Task<ActionResult<ApiResponse<AttendanceCheckResultDto>>> CheckEmployee(string employeeId)
        {
            var result = await _employeeService.CheckEmployeeStatusAsync(employeeId);
            if (!result.Success)
            {
                return NotFound(result);
            }
            return Ok(result);
        }
    }
}
