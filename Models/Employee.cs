using System;
using System.Collections.Generic;

namespace CbeMeetingAttendance.Models
{
    public class Employee
    {
        public Guid Id { get; set; }
        
        // This is the custom employee ID e.g. "EMP001" inputted by the user/employee
        public required string EmployeeId { get; set; }
        public required string FullName { get; set; }
        public required string Division { get; set; }
        public required string JobTitle { get; set; }
        public required string Department { get; set; }
        public required string PhoneNumber { get; set; }
        public DateTime CreatedAt { get; set; }

        // Navigation property for attendance records
        public ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();
    }
}
