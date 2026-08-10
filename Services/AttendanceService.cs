using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CbeMeetingAttendance.DTOs;
using CbeMeetingAttendance.Enums;
using CbeMeetingAttendance.Mappers;
using CbeMeetingAttendance.Models;
using CbeMeetingAttendance.Repositories;
using OfficeOpenXml;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace CbeMeetingAttendance.Services
{
    public class AttendanceService : IAttendanceService
    {
        private readonly IAttendanceRepository _attendanceRepository;
        private readonly IEmployeeRepository _employeeRepository;

        public AttendanceService(IAttendanceRepository attendanceRepository, IEmployeeRepository employeeRepository)
        {
            _attendanceRepository = attendanceRepository ?? throw new ArgumentNullException(nameof(attendanceRepository));
            _employeeRepository = employeeRepository ?? throw new ArgumentNullException(nameof(employeeRepository));
        }

        public async Task<ApiResponse<AttendanceCheckResultDto>> CheckAttendanceAsync(string employeeId, string? date = null, string? session = null)
        {
            if (string.IsNullOrWhiteSpace(employeeId))
            {
                return ApiResponse<AttendanceCheckResultDto>.FailureResult("Employee ID is required.");
            }

            var employee = await _employeeRepository.GetByEmployeeIdAsync(employeeId);
            if (employee == null)
            {
                return ApiResponse<AttendanceCheckResultDto>.FailureResult($"Employee with ID '{employeeId}' not found.");
            }

            DateOnly targetDate;
            if (string.IsNullOrWhiteSpace(date))
            {
                targetDate = DateOnly.FromDateTime(DateTime.Now);
            }
            else if (!DateOnly.TryParse(date, out targetDate))
            {
                return ApiResponse<AttendanceCheckResultDto>.FailureResult("Invalid date parameter. Use YYYY-MM-DD.");
            }

            Session targetSession;
            if (string.IsNullOrWhiteSpace(session))
            {
                targetSession = DateTime.Now.Hour < 12 ? Session.Morning : Session.Afternoon;
            }
            else if (!Enum.TryParse(session, true, out targetSession))
            {
                return ApiResponse<AttendanceCheckResultDto>.FailureResult("Invalid session parameter. Must be 'Morning' or 'Afternoon'.");
            }

            var attendance = await _attendanceRepository.GetAttendanceAsync(employee.Id, targetDate, targetSession);
            var result = employee.ToCheckResultDto(targetDate, targetSession, attendance);

            string statusMsg = result.IsAttended
                ? "Attendance was taken."
                : "Attendance has not been taken yet.";
            return ApiResponse<AttendanceCheckResultDto>.SuccessResult(result, statusMsg);
        }

        public async Task<ApiResponse<AttendanceDto>> TakeAttendanceAsync(TakeAttendanceDto dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));

            var employee = await _employeeRepository.GetByEmployeeIdAsync(dto.EmployeeId);
            if (employee == null)
            {
                return ApiResponse<AttendanceDto>.FailureResult($"Employee with ID '{dto.EmployeeId}' is not registered in the system.");
            }

            // Parse Date
            DateOnly targetDate;
            if (string.IsNullOrWhiteSpace(dto.MeetingDate))
            {
                targetDate = DateOnly.FromDateTime(DateTime.Now);
            }
            else
            {
                if (!DateOnly.TryParse(dto.MeetingDate, out targetDate))
                {
                    return ApiResponse<AttendanceDto>.FailureResult("Invalid MeetingDate format. Use YYYY-MM-DD.");
                }
            }

            // Parse Session
            Session targetSession;
            if (string.IsNullOrWhiteSpace(dto.Session))
            {
                targetSession = DateTime.Now.Hour < 12 ? Session.Morning : Session.Afternoon;
            }
            else
            {
                if (!Enum.TryParse(dto.Session, true, out targetSession))
                {
                    return ApiResponse<AttendanceDto>.FailureResult("Invalid Session value. Must be 'Morning' or 'Afternoon'.");
                }
            }

            // Check if attendance already exists (unique index verification)
            var existingAttendance = await _attendanceRepository.GetAttendanceAsync(employee.Id, targetDate, targetSession);
            if (existingAttendance != null)
            {
                return ApiResponse<AttendanceDto>.FailureResult(
                    $"Attendance already taken for employee '{employee.FullName}' ({dto.EmployeeId}) on {targetDate:yyyy-MM-dd} in the {targetSession} session."
                );
            }

            // Record attendance
            var attendance = new Attendance
            {
                Id = Guid.NewGuid(),
                EmployeeId = employee.Id,
                Employee = employee,
                MeetingDate = targetDate,
                Session = targetSession,
                AttendanceTime = DateTime.Now
            };

            await _attendanceRepository.AddAsync(attendance);

            return ApiResponse<AttendanceDto>.SuccessResult(attendance.ToDto(), "Attendance recorded successfully.");
        }

        public async Task<ApiResponse<DashboardDto>> GetDashboardStatsAsync()
        {
            var totalEmployees = await _employeeRepository.GetCountAsync();
            var today = DateOnly.FromDateTime(DateTime.Now);

            var morningCount = await _attendanceRepository.GetAttendanceCountAsync(today, Session.Morning);
            var afternoonCount = await _attendanceRepository.GetAttendanceCountAsync(today, Session.Afternoon);

            // Get all attendances today to compute unique present count
            var todayAttendances = await _attendanceRepository.GetAttendancesByDateAsync(today);
            var uniquePresentToday = todayAttendances.Select(a => a.EmployeeId).Distinct().Count();

            double attendanceRate = totalEmployees > 0 
                ? Math.Round((double)uniquePresentToday / totalEmployees * 100, 2) 
                : 0.0;

            var recentRecords = await _attendanceRepository.GetRecentAttendancesAsync(10);
            var recentDtos = recentRecords.Select(r => r.ToDto()).ToList();

            var stats = new DashboardDto
            {
                TotalEmployees = totalEmployees,
                TodayMorningCount = morningCount,
                TodayAfternoonCount = afternoonCount,
                TodayAttendanceRate = attendanceRate,
                RecentAttendances = recentDtos
            };

            return ApiResponse<DashboardDto>.SuccessResult(stats, "Dashboard statistics retrieved successfully.");
        }

        public async Task<FileExportResult> ExportAttendanceAsync(string format)
        {
            var attendances = (await _attendanceRepository.GetAllAsync()).ToList();
            var normalizedFormat = format.Trim().ToLower();

            if (normalizedFormat == "csv")
            {
                return GenerateCsv(attendances);
            }
            else if (normalizedFormat == "pdf")
            {
                return GeneratePdf(attendances);
            }
            else
            {
                // Default to Excel
                return GenerateExcel(attendances);
            }
        }

        private FileExportResult GenerateCsv(List<Attendance> attendances)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Employee ID,Full Name,Division,Job Title,Department,Phone Number,Meeting Date,Session,Attendance Time");

            foreach (var att in attendances)
            {
                var emp = att.Employee;
                sb.AppendLine($"\"{emp?.EmployeeId}\",\"{emp?.FullName}\",\"{emp?.Division}\",\"{emp?.JobTitle}\",\"{emp?.Department}\",\"{emp?.PhoneNumber}\",\"{att.MeetingDate:yyyy-MM-dd}\",\"{att.Session}\",\"{att.AttendanceTime:yyyy-MM-dd HH:mm:ss}\"");
            }

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            return new FileExportResult
            {
                FileBytes = bytes,
                ContentType = "text/csv",
                FileName = $"AttendanceReport_{DateTime.Now:yyyyMMddHHmmss}.csv"
            };
        }

        private FileExportResult GenerateExcel(List<Attendance> attendances)
        {
            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("Meeting Attendance");

            var headers = new[] { "Employee ID", "Full Name", "Division", "Job Title", "Department", "Phone Number", "Meeting Date", "Session", "Attendance Time" };
            for (int i = 0; i < headers.Length; i++)
            {
                worksheet.Cells[1, i + 1].Value = headers[i];
                worksheet.Cells[1, i + 1].Style.Font.Bold = true;
            }

            int rowIdx = 2;
            foreach (var att in attendances)
            {
                var emp = att.Employee;
                worksheet.Cells[rowIdx, 1].Value = emp?.EmployeeId;
                worksheet.Cells[rowIdx, 2].Value = emp?.FullName;
                worksheet.Cells[rowIdx, 3].Value = emp?.Division;
                worksheet.Cells[rowIdx, 4].Value = emp?.JobTitle;
                worksheet.Cells[rowIdx, 5].Value = emp?.Department;
                worksheet.Cells[rowIdx, 6].Value = emp?.PhoneNumber;
                worksheet.Cells[rowIdx, 7].Value = att.MeetingDate.ToString("yyyy-MM-dd");
                worksheet.Cells[rowIdx, 8].Value = att.Session.ToString();
                worksheet.Cells[rowIdx, 9].Value = att.AttendanceTime.ToString("yyyy-MM-dd HH:mm:ss");
                rowIdx++;
            }

            worksheet.Cells.AutoFitColumns();

            return new FileExportResult
            {
                FileBytes = package.GetAsByteArray(),
                ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                FileName = $"AttendanceReport_{DateTime.Now:yyyyMMddHHmmss}.xlsx"
            };
        }

        private FileExportResult GeneratePdf(List<Attendance> attendances)
        {
            var document = new PdfDocument();
            document.Info.Title = "CBE Meeting Attendance Report";

            // Define layouts and constants
            const double margin = 40;
            const double pageWidth = 595;  // A4 dimensions
            const double pageHeight = 842;
            const double rowHeight = 22;
            const double headerHeight = 110;
            const double footerHeight = 40;
            const double bodyHeight = pageHeight - headerHeight - footerHeight;
            const int rowsPerPage = (int)(bodyHeight / rowHeight);

            // Columns and widths
            var colWidths = new[] { 65.0, 105.0, 95.0, 85.0, 60.0, 105.0 }; // Total = 515 (595 - 80 margins)
            var colHeaders = new[] { "Emp ID", "Full Name", "Department", "Meeting Date", "Session", "Attended Time" };

            int totalPages = (int)Math.Ceiling((double)attendances.Count / rowsPerPage);
            if (totalPages == 0) totalPages = 1;

            // Font loading
            var fontTitle = new XFont("Arial", 16, XFontStyleEx.Bold);
            var fontSubtitle = new XFont("Arial", 9, XFontStyleEx.Italic);
            var fontHeader = new XFont("Arial", 10, XFontStyleEx.Bold);
            var fontBody = new XFont("Arial", 9, XFontStyleEx.Regular);
            var fontFooter = new XFont("Arial", 8, XFontStyleEx.Regular);

            // Colors and Pens
            var brushPrimary = new XSolidBrush(XColor.FromArgb(16, 44, 87)); // Dark Navy
            var brushSecondary = new XSolidBrush(XColor.FromArgb(240, 240, 240)); // light gray for header/zebra
            var penGrid = new XPen(XColor.FromArgb(220, 220, 220), 0.75);

            for (int pageIdx = 0; pageIdx < totalPages; pageIdx++)
            {
                var page = document.AddPage();
                var gfx = XGraphics.FromPdfPage(page);

                // --- HEADER DRAWING ---
                // Header background block
                gfx.DrawRectangle(brushPrimary, margin, margin, pageWidth - (margin * 2), 40);
                
                // Title
                gfx.DrawString("CBE MEETING ATTENDANCE REPORT", fontTitle, XBrushes.White, 
                    new XRect(margin, margin, pageWidth - (margin * 2), 40), XStringFormats.Center);

                // Subtitle/Metadata
                string metadataStr = $"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss} | Total records: {attendances.Count}";
                gfx.DrawString(metadataStr, fontSubtitle, XBrushes.DimGray, 
                    new XPoint(margin, margin + 55));

                // --- TABLE HEADERS ---
                double tableHeaderY = margin + 65;
                gfx.DrawRectangle(brushSecondary, margin, tableHeaderY, pageWidth - (margin * 2), 20);
                gfx.DrawRectangle(penGrid, margin, tableHeaderY, pageWidth - (margin * 2), 20);

                double currentX = margin;
                for (int colIdx = 0; colIdx < colHeaders.Length; colIdx++)
                {
                    var colRect = new XRect(currentX, tableHeaderY, colWidths[colIdx], 20);
                    gfx.DrawString(colHeaders[colIdx], fontHeader, XBrushes.Black, colRect, XStringFormats.Center);
                    currentX += colWidths[colIdx];
                }

                // --- TABLE DATA ---
                int startItemIdx = pageIdx * rowsPerPage;
                int endItemIdx = Math.Min(startItemIdx + rowsPerPage, attendances.Count);
                double currentY = tableHeaderY + 20;

                for (int itemIdx = startItemIdx; itemIdx < endItemIdx; itemIdx++)
                {
                    var att = attendances[itemIdx];
                    var emp = att.Employee;

                    // Zebra striping background
                    if (itemIdx % 2 == 1)
                    {
                        gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(250, 252, 254)), 
                            margin, currentY, pageWidth - (margin * 2), rowHeight);
                    }

                    // Draw border line
                    gfx.DrawLine(penGrid, margin, currentY + rowHeight, pageWidth - margin, currentY + rowHeight);

                    // Row data fields
                    var dataFields = new[]
                    {
                        emp?.EmployeeId ?? string.Empty,
                        emp?.FullName ?? string.Empty,
                        emp?.Department ?? string.Empty,
                        att.MeetingDate.ToString("yyyy-MM-dd"),
                        att.Session.ToString(),
                        att.AttendanceTime.ToString("yyyy-MM-dd HH:mm")
                    };

                    double dataX = margin;
                    for (int cellIdx = 0; cellIdx < dataFields.Length; cellIdx++)
                    {
                        // Draw grid vertical lines
                        if (cellIdx > 0)
                        {
                            gfx.DrawLine(penGrid, dataX, currentY, dataX, currentY + rowHeight);
                        }

                        // Set a padding rect for printing text
                        var padRect = new XRect(dataX + 4, currentY, colWidths[cellIdx] - 8, rowHeight);
                        gfx.DrawString(TruncateString(dataFields[cellIdx], 20), fontBody, XBrushes.Black, padRect, XStringFormats.CenterLeft);
                        dataX += colWidths[cellIdx];
                    }

                    currentY += rowHeight;
                }

                // Outer border lines
                gfx.DrawLine(penGrid, margin, tableHeaderY, margin, currentY);
                gfx.DrawLine(penGrid, pageWidth - margin, tableHeaderY, pageWidth - margin, currentY);

                // --- FOOTER DRAWING ---
                double footerY = pageHeight - footerHeight;
                gfx.DrawLine(new XPen(XColor.FromArgb(100, 100, 100), 0.5), margin, footerY, pageWidth - margin, footerY);
                
                string footerText = "Commercial Bank of Ethiopia (CBE) - Confidential";
                gfx.DrawString(footerText, fontFooter, XBrushes.Gray, new XPoint(margin, footerY + 15));

                string pageText = $"Page {pageIdx + 1} of {totalPages}";
                gfx.DrawString(pageText, fontFooter, XBrushes.Gray, 
                    new XRect(margin, footerY, pageWidth - (margin * 2), footerHeight), XStringFormats.CenterRight);
            }

            using var stream = new MemoryStream();
            document.Save(stream);
            var bytes = stream.ToArray();

            return new FileExportResult
            {
                FileBytes = bytes,
                ContentType = "application/pdf",
                FileName = $"AttendanceReport_{DateTime.Now:yyyyMMddHHmmss}.pdf"
            };
        }

        private string TruncateString(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Length <= maxLength ? value : value.Substring(0, maxLength - 3) + "...";
        }
    }
}
