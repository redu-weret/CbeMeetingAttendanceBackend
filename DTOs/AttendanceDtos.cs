using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace CbeMeetingAttendance.DTOs
{
    public class AttendanceDto
    {
        public Guid Id { get; set; }
        public Guid EmployeeId { get; set; }
        public required string EmployeeUniqueId { get; set; } // Custom EmployeeId e.g. "EMP001"
        public required string EmployeeFullName { get; set; }
        public required string Department { get; set; }
        public required string MeetingDate { get; set; } // Format YYYY-MM-DD
        public required string Session { get; set; } // Morning / Afternoon
        public DateTime AttendanceTime { get; set; }
    }

    public class TakeAttendanceDto
    {
        [Required(ErrorMessage = "EmployeeId (the string employee code) is required")]
        public required string EmployeeId { get; set; } // The custom string EmployeeId (e.g. "EMP001")
        
        // Optional override: if omitted, system date (today) is used
        public string? MeetingDate { get; set; } // Format YYYY-MM-DD

        // Optional override: if omitted, system session is determined automatically based on time
        public string? Session { get; set; } // Morning / Afternoon
    }

    public class AttendanceCheckResultDto
    {
        public Guid Id { get; set; }
        public required string EmployeeId { get; set; }
        public required string FullName { get; set; }
        public required string Division { get; set; }
        public required string JobTitle { get; set; }
        public required string Department { get; set; }
        public required string PhoneNumber { get; set; }
        public required string MeetingDate { get; set; }
        public required string Session { get; set; }
        public bool IsAttended { get; set; }
        public DateTime? AttendanceTime { get; set; }
    }

    public class FileExportResult
    {
        public required byte[] FileBytes { get; set; }
        public required string ContentType { get; set; }
        public required string FileName { get; set; }
    }

    public class DashboardDto
    {
        public int TotalEmployees { get; set; }
        public int TodayMorningCount { get; set; }
        public int TodayAfternoonCount { get; set; }
        public double TodayAttendanceRate { get; set; } // Percentage of registered employees present today
        public List<AttendanceDto> RecentAttendances { get; set; } = new List<AttendanceDto>();
    }
}
