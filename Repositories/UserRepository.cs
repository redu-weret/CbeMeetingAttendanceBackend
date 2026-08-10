using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CbeMeetingAttendance.Data;
using CbeMeetingAttendance.Models;
using Microsoft.EntityFrameworkCore;

namespace CbeMeetingAttendance.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly ApplicationDbContext _context;

        public UserRepository(ApplicationDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<User?> GetByIdAsync(Guid id)
        {
            return await _context.Users
                .Include(u => u.Employee)
                .FirstOrDefaultAsync(u => u.Id == id);
        }

        public async Task<IEnumerable<User>> GetAllAsync()
        {
            return await _context.Users
                .Include(u => u.Employee)
                .OrderBy(u => u.UserName)
                .ToListAsync();
        }

        public async Task<User?> GetByEmailAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return null;

            return await _context.Users
                .Include(u => u.Employee)
                .FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == email.Trim().ToLower());
        }

        public async Task<User?> GetByUserNameAsync(string userName)
        {
            if (string.IsNullOrWhiteSpace(userName)) return null;

            return await _context.Users
                .Include(u => u.Employee)
                .FirstOrDefaultAsync(u => u.UserName != null && u.UserName.ToLower() == userName.Trim().ToLower());
        }

        public async Task<bool> ExistsByEmailAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return false;

            return await _context.Users
                .AnyAsync(u => u.Email != null && u.Email.ToLower() == email.Trim().ToLower());
        }

        public async Task<bool> ExistsByEmployeeIdAsync(Guid employeeId)
        {
            return await _context.Users.AnyAsync(u => u.EmployeeId == employeeId);
        }

        public async Task<int> GetCountAsync()
        {
            return await _context.Users.CountAsync();
        }

        public async Task<Dictionary<Guid, string?>> GetProfilePicturePathsByEmployeeIdsAsync(IEnumerable<Guid> employeeIds)
        {
            var ids = employeeIds.ToList();
            if (ids.Count == 0)
            {
                return new Dictionary<Guid, string?>();
            }

            return await _context.Users
                .Where(u => ids.Contains(u.EmployeeId))
                .Select(u => new { u.EmployeeId, u.ProfilePicturePath })
                .ToDictionaryAsync(x => x.EmployeeId, x => x.ProfilePicturePath);
        }
    }
}
