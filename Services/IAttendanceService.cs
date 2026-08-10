using System.Threading.Tasks;
using CbeMeetingAttendance.DTOs;

namespace CbeMeetingAttendance.Services
{
    public interface IAttendanceService
    {
        Task<ApiResponse<AttendanceCheckResultDto>> CheckAttendanceAsync(string employeeId, string? date = null, string? session = null);
        Task<ApiResponse<AttendanceDto>> TakeAttendanceAsync(TakeAttendanceDto dto);
        Task<ApiResponse<DashboardDto>> GetDashboardStatsAsync();
        Task<FileExportResult> ExportAttendanceAsync(string format);
    }
}
