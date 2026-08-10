using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CbeMeetingAttendance.Data;
using CbeMeetingAttendance.Enums;
using CbeMeetingAttendance.Models;
using Microsoft.EntityFrameworkCore;

namespace CbeMeetingAttendance.Repositories
{
    public class AttendanceRepository : IAttendanceRepository
    {
        private readonly ApplicationDbContext _context;

        public AttendanceRepository(ApplicationDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<Attendance?> GetAttendanceAsync(Guid employeeId, DateOnly date, Session session)
        {
            return await _context.Attendances
                .Include(a => a.Employee)
                .FirstOrDefaultAsync(a => a.EmployeeId == employeeId && a.MeetingDate == date && a.Session == session);
        }

        public async Task<Attendance?> GetAttendanceByEmployeeCodeAsync(string employeeId, DateOnly date, Session session)
        {
            if (string.IsNullOrWhiteSpace(employeeId)) return null;

            return await _context.Attendances
                .Include(a => a.Employee)
                .FirstOrDefaultAsync(a => a.Employee.EmployeeId.ToLower() == employeeId.Trim().ToLower() 
                                          && a.MeetingDate == date 
                                          && a.Session == session);
        }

        public async Task AddAsync(Attendance attendance)
        {
            if (attendance == null) throw new ArgumentNullException(nameof(attendance));
            
            await _context.Attendances.AddAsync(attendance);
            await _context.SaveChangesAsync();
        }

        public async Task<IEnumerable<Attendance>> GetAttendancesByDateAsync(DateOnly date)
        {
            return await _context.Attendances
                .Include(a => a.Employee)
                .Where(a => a.MeetingDate == date)
                .OrderByDescending(a => a.AttendanceTime)
                .ToListAsync();
        }

        public async Task<IEnumerable<Attendance>> GetAllAsync()
        {
            return await _context.Attendances
                .Include(a => a.Employee)
                .OrderByDescending(a => a.AttendanceTime)
                .ToListAsync();
        }

        public async Task<int> GetAttendanceCountAsync(DateOnly date, Session session)
        {
            return await _context.Attendances
                .CountAsync(a => a.MeetingDate == date && a.Session == session);
        }

        public async Task<List<Attendance>> GetRecentAttendancesAsync(int limit)
        {
            return await _context.Attendances
                .Include(a => a.Employee)
                .OrderByDescending(a => a.AttendanceTime)
                .Take(limit)
                .ToListAsync();
        }
    }
}
