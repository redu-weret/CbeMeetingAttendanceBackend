using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CbeMeetingAttendance.Models;

namespace CbeMeetingAttendance.Repositories
{
    public interface IEmployeeRepository
    {
        Task<Employee?> GetByIdAsync(Guid id);
        Task<Employee?> GetByEmployeeIdAsync(string employeeId);
        Task<IEnumerable<Employee>> GetAllAsync();
        Task AddAsync(Employee employee);
        Task UpdateAsync(Employee employee);
        Task DeleteAsync(Employee employee);
        Task<bool> ExistsAsync(string employeeId);
        Task<int> GetCountAsync();
    }
}
