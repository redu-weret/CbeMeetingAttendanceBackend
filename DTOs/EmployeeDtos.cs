using System;
using System.ComponentModel.DataAnnotations;

namespace CbeMeetingAttendance.DTOs
{
    public class EmployeeDto
    {
        public Guid Id { get; set; }
        public required string EmployeeId { get; set; }
        public required string FullName { get; set; }
        public required string Division { get; set; }
        public required string JobTitle { get; set; }
        public required string Department { get; set; }
        public required string PhoneNumber { get; set; }
        public DateTime CreatedAt { get; set; }

        // Relative path (e.g. "Profiles/8f23c7d4.jpg") of the linked user's
        // profile picture, if a user account exists for this employee.
        public string? ProfilePicturePath { get; set; }
    }

    public class RegisterEmployeeDto
    {
        [Required(ErrorMessage = "EmployeeId is required")]
        [StringLength(50, ErrorMessage = "EmployeeId cannot exceed 50 characters")]
        public required string EmployeeId { get; set; }

        [Required(ErrorMessage = "FullName is required")]
        [StringLength(100, ErrorMessage = "FullName cannot exceed 100 characters")]
        public required string FullName { get; set; }

        [Required(ErrorMessage = "Division is required")]
        [StringLength(100, ErrorMessage = "Division cannot exceed 100 characters")]
        public required string Division { get; set; }

        [Required(ErrorMessage = "JobTitle is required")]
        [StringLength(100, ErrorMessage = "JobTitle cannot exceed 100 characters")]
        public required string JobTitle { get; set; }

        [Required(ErrorMessage = "Department is required")]
        [StringLength(100, ErrorMessage = "Department cannot exceed 100 characters")]
        public required string Department { get; set; }

        [Required(ErrorMessage = "PhoneNumber is required")]
        [Phone(ErrorMessage = "Invalid phone number format")]
        public required string PhoneNumber { get; set; }
    }

    public class UpdateEmployeeDto
    {
        [Required(ErrorMessage = "FullName is required")]
        [StringLength(100, ErrorMessage = "FullName cannot exceed 100 characters")]
        public required string FullName { get; set; }

        [Required(ErrorMessage = "Division is required")]
        [StringLength(100, ErrorMessage = "Division cannot exceed 100 characters")]
        public required string Division { get; set; }

        [Required(ErrorMessage = "JobTitle is required")]
        [StringLength(100, ErrorMessage = "JobTitle cannot exceed 100 characters")]
        public required string JobTitle { get; set; }

        [Required(ErrorMessage = "Department is required")]
        [StringLength(100, ErrorMessage = "Department cannot exceed 100 characters")]
        public required string Department { get; set; }

        [Required(ErrorMessage = "PhoneNumber is required")]
        [Phone(ErrorMessage = "Invalid phone number format")]
        public required string PhoneNumber { get; set; }
    }
}
