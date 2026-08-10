using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CbeMeetingAttendance.Data;
using CbeMeetingAttendance.Models;
using Microsoft.EntityFrameworkCore;

namespace CbeMeetingAttendance.Repositories
{
    public class EmployeeRepository : IEmployeeRepository
    {
        private readonly ApplicationDbContext _context;

        public EmployeeRepository(ApplicationDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<Employee?> GetByIdAsync(Guid id)
        {
            return await _context.Employees.FindAsync(id);
        }

        public async Task<Employee?> GetByEmployeeIdAsync(string employeeId)
        {
            if (string.IsNullOrWhiteSpace(employeeId)) return null;
            
            return await _context.Employees
                .FirstOrDefaultAsync(e => e.EmployeeId.ToLower() == employeeId.Trim().ToLower());
        }

        public async Task<IEnumerable<Employee>> GetAllAsync()
        {
            return await _context.Employees
                .OrderByDescending(e => e.CreatedAt)
                .ToListAsync();
        }

        public async Task AddAsync(Employee employee)
        {
            if (employee == null) throw new ArgumentNullException(nameof(employee));
            
            await _context.Employees.AddAsync(employee);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Employee employee)
        {
            if (employee == null) throw new ArgumentNullException(nameof(employee));
            
            _context.Employees.Update(employee);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(Employee employee)
        {
            if (employee == null) throw new ArgumentNullException(nameof(employee));
            
            _context.Employees.Remove(employee);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> ExistsAsync(string employeeId)
        {
            if (string.IsNullOrWhiteSpace(employeeId)) return false;
            
            return await _context.Employees
                .AnyAsync(e => e.EmployeeId.ToLower() == employeeId.Trim().ToLower());
        }

        public async Task<int> GetCountAsync()
        {
            return await _context.Employees.CountAsync();
        }
    }
}
