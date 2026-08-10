using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CbeMeetingAttendance.Enums;
using CbeMeetingAttendance.Models;

namespace CbeMeetingAttendance.Repositories
{
    public interface IAttendanceRepository
    {
        Task<Attendance?> GetAttendanceAsync(Guid employeeId, DateOnly date, Session session);
        Task<Attendance?> GetAttendanceByEmployeeCodeAsync(string employeeId, DateOnly date, Session session);
        Task AddAsync(Attendance attendance);
        Task<IEnumerable<Attendance>> GetAttendancesByDateAsync(DateOnly date);
        Task<IEnumerable<Attendance>> GetAllAsync();
        Task<int> GetAttendanceCountAsync(DateOnly date, Session session);
        Task<List<Attendance>> GetRecentAttendancesAsync(int limit);
    }
}
