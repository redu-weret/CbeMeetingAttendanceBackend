using System;
using Microsoft.AspNetCore.Identity;

namespace CbeMeetingAttendance.Models
{
    public class User : IdentityUser<Guid>
    {
        // Foreign key to Employee.Id — other fields come from IdentityUser
        public Guid EmployeeId { get; set; }
        public Employee Employee { get; set; } = null!;

        // Profile picture: byte[] persisted to SQL Server (varbinary(max))
        // plus a relative path (e.g. "Profiles/8f23c7d4.jpg") to the physical copy in wwwroot/Profiles
        public byte[]? ProfilePicture { get; set; }
        public string? ProfilePicturePath { get; set; }
    }
}
