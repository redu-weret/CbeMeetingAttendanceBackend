using System;
using System.Collections.Generic;
using CbeMeetingAttendance.DTOs;
using CbeMeetingAttendance.Enums;
using CbeMeetingAttendance.Models;

namespace CbeMeetingAttendance.Mappers
{
    public static class MappingExtensions
    {
        public static EmployeeDto ToDto(this Employee employee)
        {
            if (employee == null) throw new ArgumentNullException(nameof(employee));

            return new EmployeeDto
            {
                Id = employee.Id,
                EmployeeId = employee.EmployeeId,
                FullName = employee.FullName,
                Division = employee.Division,
                JobTitle = employee.JobTitle,
                Department = employee.Department,
                PhoneNumber = employee.PhoneNumber,
                CreatedAt = employee.CreatedAt
            };
        }

        public static Employee ToEntity(this RegisterEmployeeDto dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));

            return new Employee
            {
                Id = Guid.NewGuid(),
                EmployeeId = dto.EmployeeId.Trim(),
                FullName = dto.FullName.Trim(),
                Division = dto.Division.Trim(),
                JobTitle = dto.JobTitle.Trim(),
                Department = dto.Department.Trim(),
                PhoneNumber = dto.PhoneNumber.Trim(),
                CreatedAt = DateTime.Now
            };
        }

        public static AttendanceDto ToDto(this Attendance attendance)
        {
            if (attendance == null) throw new ArgumentNullException(nameof(attendance));

            return new AttendanceDto
            {
                Id = attendance.Id,
                EmployeeId = attendance.EmployeeId,
                EmployeeUniqueId = attendance.Employee?.EmployeeId ?? string.Empty,
                EmployeeFullName = attendance.Employee?.FullName ?? string.Empty,
                Department = attendance.Employee?.Department ?? string.Empty,
                MeetingDate = attendance.MeetingDate.ToString("yyyy-MM-dd"),
                Session = attendance.Session.ToString(),
                AttendanceTime = attendance.AttendanceTime
            };
        }

        public static AttendanceCheckResultDto ToCheckResultDto(
            this Employee employee,
            DateOnly meetingDate,
            Session session,
            Attendance? attendance)
        {
            if (employee == null) throw new ArgumentNullException(nameof(employee));

            return new AttendanceCheckResultDto
            {
                Id = employee.Id,
                EmployeeId = employee.EmployeeId,
                FullName = employee.FullName,
                Division = employee.Division,
                JobTitle = employee.JobTitle,
                Department = employee.Department,
                PhoneNumber = employee.PhoneNumber,
                MeetingDate = meetingDate.ToString("yyyy-MM-dd"),
                Session = session.ToString(),
                IsAttended = attendance != null,
                AttendanceTime = attendance?.AttendanceTime
            };
        }

        public static UserDto ToDto(this User user, IList<string> roles)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));

            return new UserDto
            {
                Id = user.Id,
                Email = user.Email,
                UserName = user.UserName,
                EmployeeId = user.EmployeeId,
                Employee = user.Employee?.ToDto(),
                Roles = roles,
                LockoutEnabled = user.LockoutEnabled,
                LockoutEnd = user.LockoutEnd,
                AccessFailedCount = user.AccessFailedCount,
                ProfilePicturePath = user.ProfilePicturePath
            };
        }

        public static CurrentUserDto ToCurrentUserDto(this User user, IList<string> roles, Employee? employee)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));

            var emp = employee ?? user.Employee;

            return new CurrentUserDto
            {
                UserId = user.Id,
                Email = user.Email,
                UserName = user.UserName,
                EmployeeId = user.EmployeeId,
                Employee = emp?.ToDto(),
                Roles = roles,
                ProfilePicturePath = user.ProfilePicturePath
            };
        }
    }
}
