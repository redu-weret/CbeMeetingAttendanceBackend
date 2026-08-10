using System;
using CbeMeetingAttendance.Enums;

namespace CbeMeetingAttendance.Models
{
    public class Attendance
    {
        public Guid Id { get; set; }
        
        // FK to Employee Guid Id
        public Guid EmployeeId { get; set; }
        public Employee Employee { get; set; } = null!;

        // Stores date like 2026-07-13, set automatically
        public DateOnly MeetingDate { get; set; }
        
        // Session enum: Morning / Afternoon
        public Session Session { get; set; }
        
        // DateTime when attendance is taken, set automatically
        public DateTime AttendanceTime { get; set; }
    }
}
