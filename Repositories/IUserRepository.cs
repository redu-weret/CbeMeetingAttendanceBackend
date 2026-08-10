using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CbeMeetingAttendance.Models;

namespace CbeMeetingAttendance.Repositories
{
    public interface IUserRepository
    {
        Task<User?> GetByIdAsync(Guid id);
        Task<IEnumerable<User>> GetAllAsync();
        Task<User?> GetByEmailAsync(string email);
        Task<User?> GetByUserNameAsync(string userName);
        Task<bool> ExistsByEmailAsync(string email);
        Task<bool> ExistsByEmployeeIdAsync(Guid employeeId);
        Task<int> GetCountAsync();

        /// Returns a map of EmployeeId → ProfilePicturePath for users linked to the given employees.
        Task<Dictionary<Guid, string?>> GetProfilePicturePathsByEmployeeIdsAsync(IEnumerable<Guid> employeeIds);
    }
}
