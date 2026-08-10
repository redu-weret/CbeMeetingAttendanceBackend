# CbeMeetingAttendance API

ASP.NET Core Web API (controller-based) for:

1. Employee registration and meeting attendance
2. User management with ASP.NET Core Identity + JWT authentication

Uses SQL Server, layered architecture, and Swagger UI for testing.

---

## Quick Start

```powershell
cd C:\Users\LEUL\Desktop\dotnet\CbeMeetingAttendance
dotnet run
```

Open Swagger: **http://localhost:5151/swagger**

**Requirements:** .NET 10 SDK, SQL Server Express (`.\SQLEXPRESS01`)

---

## Architecture

```
Request → Controller → Service → Repository → Database (SQL Server)
                ↓           ↓
              DTOs      Mappers
```

| Layer | Responsibility | Folder |
|-------|----------------|--------|
| **Controller** | Accept HTTP request, return HTTP response only | `Controllers/` |
| **Service** | All business logic (validation, JWT, lockout, roles, attendance) | `Services/` |
| **Repository** | All database access via EF Core | `Repositories/` |
| **Model** | Database table structure | `Models/` |
| **DTO** | API request/response shapes (never expose models directly) | `DTOs/` |

Identity (`UserManager` / `RoleManager`) is used inside the **Service** layer for password hashing, lockout, roles, and tokens.

---

## Project Structure — What Each File Does

```
CbeMeetingAttendance/
│
├── Program.cs                          # App entry: Identity, JWT, DI, Swagger, migrations
├── appsettings.json                    # SQL connection + JwtSettings
├── appsettings.Development.json        # Development logging settings
├── CbeMeetingAttendance.csproj         # Project file and NuGet packages
├── CbeMeetingAttendance.http           # Optional HTTP test file for VS Code
├── README.md                           # This file
│
├── Properties/
│   └── launchSettings.json             # Run URL (port 5151) and Swagger launch
│
├── Controllers/                        # API endpoints (what Swagger shows)
│   ├── EmployeeController.cs           # Employee CRUD + check employee
│   ├── AttendanceController.cs         # Attendance + admin dashboard + export
│   └── UserController.cs               # Register, login, logout, refresh, roles, passwords
│
├── Services/                           # Business logic layer
│   ├── IEmployeeService.cs             # Employee service contract
│   ├── EmployeeService.cs              # Employee registration, update, delete, check
│   ├── IAttendanceService.cs           # Attendance service contract
│   ├── AttendanceService.cs            # Attendance, dashboard, Excel/CSV/PDF export
│   ├── IUserService.cs                 # User/auth service contract
│   ├── UserService.cs                  # Identity login, JWT, roles, password, lockout
│   └── WindowsFontResolver.cs          # Loads Arial fonts for PDF export on Windows
│
├── Repositories/                       # Database access layer
│   ├── IEmployeeRepository.cs          # Employee repository contract
│   ├── EmployeeRepository.cs           # Employee CRUD queries
│   ├── IAttendanceRepository.cs        # Attendance repository contract
│   ├── AttendanceRepository.cs         # Attendance queries and counts
│   ├── IUserRepository.cs              # User repository contract
│   └── UserRepository.cs               # User queries (get all, get by id/email, exists)
│
├── Models/                             # Database entities (EF Core / Identity)
│   ├── Employee.cs                     # Employee table model
│   ├── Attendance.cs                   # Attendance table model
│   └── User.cs                         # IdentityUser<Guid> + EmployeeId FK
│
├── DTOs/                               # Data Transfer Objects for API
│   ├── ApiResponse.cs                  # Success, Message, Total, Data
│   ├── EmployeeDtos.cs                 # EmployeeDto, RegisterEmployeeDto, UpdateEmployeeDto
│   ├── AttendanceDtos.cs               # AttendanceDto, TakeAttendanceDto, DashboardDto, etc.
│   └── AuthDtos.cs                     # Login, RegisterUser, AuthResponseDto, roles, passwords
│
├── Data/
│   └── ApplicationDbContext.cs         # IdentityDbContext + Employees + Attendances
│
├── Enums/
│   └── Session.cs                      # Morning, Afternoon
│
├── Mappers/
│   └── MappingExtensions.cs            # Model ↔ DTO helpers (Employee, Attendance, User)
│
├── Middleware/
│   └── ExceptionHandlingMiddleware.cs  # Catches unhandled errors → ApiResponse JSON
│
└── Migrations/
    ├── 20260714133209_InitialCreate.cs              # Employees + Attendances
    ├── 20260716140523_AddIdentityUserManagement.cs  # AspNetUsers, Roles, Tokens, etc.
    └── ApplicationDbContextModelSnapshot.cs
```

---

## What Was Added — User Management (and why)

| File / Setting | What it is | Why it exists / how it is used |
|----------------|------------|--------------------------------|
| `Models/User.cs` | `User : IdentityUser<Guid>` with only `EmployeeId` FK | Built-in Identity fields (Id, Email, UserName, PasswordHash, Lockout…) come from the parent. We only add the link to Employee. |
| `DTOs/AuthDtos.cs` | Register/Login/Refresh/Role/Password DTOs + `AuthResponseDto` + `CurrentUserDto` | Request/response shapes for user APIs. Login returns JWT + employee info. |
| `DTOs/ApiResponse.cs` | Added `Total` property | Used for list endpoints (e.g. GetAllUsers returns how many users). |
| `Repositories/IUserRepository.cs` + `UserRepository.cs` | DB queries for users | Get by id/email/username, list users, check if email/employee already has a user. |
| `Services/IUserService.cs` + `UserService.cs` | All auth business logic | Checks user exists, AccessFailedCount, lockout, ResetAccessFailedCount, JWT generation, refresh token, roles, change/reset password. |
| `Controllers/UserController.cs` | HTTP endpoints only | Accepts request → calls service → returns response. No business logic. |
| `Data/ApplicationDbContext.cs` | Now `IdentityDbContext<User, IdentityRole<Guid>, Guid>` | Creates Identity tables (`AspNetUsers`, `AspNetRoles`, …) and FK User → Employee. |
| `Program.cs` | Identity + JWT + `UseAuthentication` + Swagger Bearer | Password rules, lockout, JWT issuer/audience/key, authorize button in Swagger. |
| `appsettings.json` → `JwtSettings` | Key, Issuer, Audiences, ExpiryMinutes | Used when creating and validating JWT tokens. |
| Migration `AddIdentityUserManagement` | Identity tables in SQL Server | Applied automatically on startup / `dotnet ef database update`. |

### Identity configuration (in `Program.cs`)

| Setting | Value | Purpose |
|---------|-------|---------|
| Password min length | 6 | Shortest allowed password |
| Require uppercase | true | Must contain A–Z |
| Require lowercase | true | Must contain a–z |
| Unique email | true | Two users cannot share the same email |
| Lockout enabled | true | Too many failed logins locks the account |
| MaxFailedAccessAttempts | 5 | Lock after 5 wrong passwords |
| DefaultLockoutTimeSpan | 15 minutes | How long the account stays locked |

### JWT settings (in `appsettings.json`)

```json
"JwtSettings": {
  "Key": "cbe",
  "Issuer": "CbeMeetingAttendance",
  "Audiences": [ "CBE-Attendance-Mobile-App", "CBE-Attendance-Web-App"],
  "ExpiryMinutes": 15
}
```

| Setting | Use |
|---------|-----|
| `Key` | Signs the token (hashed to 256-bit key for HMAC-SHA256) |
| `Issuer` | Who created the token |
| `Audiences` | Which apps are allowed to use the token |
| `ExpiryMinutes` | Token lifetime (15 minutes). Login uses this value. |

The project now also supports an optional `AdminUser` bootstrap section in `appsettings.json`.
If the database has no users, the configured admin account is created automatically on startup.

JWT also includes claim **`EmployeeId`** (the employee Guid FK).

---

## File Details (core + user management)

### `Program.cs`
- Registers SQL Server `DbContext`
- Configures **ASP.NET Core Identity** (password + lockout)
- Configures **JWT Bearer** authentication from `JwtSettings`
- Registers repositories and services
- Enables OpenAPI + Swagger **Authorize (Bearer)**
- Calls `UseAuthentication()` then `UseAuthorization()`
- Applies migrations on startup

### `appsettings.json`
- Connection string → database `CbeMeetingAttendance`
- `JwtSettings` → JWT creation/validation

### Controllers

#### `EmployeeController.cs`
| Method | Route | What it does |
|--------|-------|--------------|
| POST | `/api/employees/register` | Register a new employee |
| GET | `/api/employees` | Get all employees |
| GET | `/api/employee/{id}` | Get employee by **Guid** |
| PUT | `/api/employee/{id}` | Update employee by Guid |
| DELETE | `/api/employee/{id}` | Delete employee by Guid |
| GET | `/api/employees/{employeeId}` | Check by **string code** + attendance status |

> Guid → `/api/employee/{id}`  
> String code like `EMP-DEMO` → `/api/employees/{employeeId}`

#### `AttendanceController.cs`
| Method | Route | What it does |
|--------|-------|--------------|
| GET | `/api/attendance/check` | Check attendance |
| POST | `/api/attendance` | Take attendance |
| GET | `/api/admin/dashboard` | Dashboard stats |
| GET | `/api/admin/export/excel?format=excel\|csv\|pdf` | Export report |

#### `UserController.cs`
| Method | Route | Auth? | What it does |
|--------|-------|-------|--------------|
| POST | `/api/users/register` | No | Create Identity user linked to Employee |
| GET | `/api/users` | **Yes** | List all users |
| GET | `/api/users/{id}` | **Yes** | Get one user by Guid |
| POST | `/api/users/login` | No | Login → JWT + refresh + employee info |
| POST | `/api/users/logout` | **Yes** | Invalidate refresh token |
| POST | `/api/users/refresh` | No | New JWT using refresh token |
| POST | `/api/users/assign-role` | **Yes** | Assign role (creates role if missing) |
| POST | `/api/users/change-role` | **Yes** | Replace one role with another |
| POST | `/api/users/remove-role` | **Yes** | Remove a role |
| POST | `/api/users/change-password` | **Yes** | Change password (needs current password) |
| POST | `/api/users/reset-password` | **Yes** | Reset password (admin-style, no old password) |

### Services

#### `UserService.cs` (business logic for users)
- Checks employee exists before register
- Checks email/username/employee not already used
- On failed login: `AccessFailedAsync` (counts failures)
- On lockout: returns locked message
- On success: `ResetAccessFailedCountAsync` + generate JWT + store refresh token
- Role assign / change / remove via `UserManager` + `RoleManager`
- ChangePassword / ResetPassword via Identity APIs

### Models

#### `User.cs`
```csharp
public class User : IdentityUser<Guid>
{
    public Guid EmployeeId { get; set; }   // FK → Employees.Id
    public Employee Employee { get; set; }
}
```

Id, UserName, Email, PasswordHash, LockoutEnd, AccessFailedCount, etc. come from `IdentityUser`.

### Auth DTOs (`AuthDtos.cs`)

| DTO | Used for |
|-----|----------|
| `RegisterUserDto` | Body of register user |
| `LoginDto` | Body of login |
| `RefreshTokenDto` | Body of refresh |
| `AssignRoleDto` / `ChangeRoleDto` / `RemoveRoleDto` | Role endpoints |
| `ChangePasswordDto` / `ResetPasswordDto` | Password endpoints |
| `AuthResponseDto` | Login/refresh data: Token, RefreshToken, Expiration, User |
| `CurrentUserDto` | User + linked Employee info inside login response |
| `UserDto` | GetAllUsers / GetUserById / Register response |

### Login response format

```json
{
  "success": true,
  "message": "Login successful.",
  "total": null,
  "data": {
    "token": "eyJhbGciOiJIUzI1NiIs...",
    "refreshToken": "...",
    "expiration": "2026-07-16T14:30:00Z",
    "user": {
      "userId": "...",
      "email": "sara@cbe.et",
      "userName": "sara",
      "employeeId": "...",
      "employee": { "fullName": "Sara Alemu", "department": "HR", ... },
      "roles": []
    }
  }
}
```

`data` type is **`AuthResponseDto`**.

---

## Database

| Setting | Value |
|---------|-------|
| Server | `.\SQLEXPRESS01` |
| Database | `CbeMeetingAttendance` |
| Tables | `Employees`, `Attendances`, `AspNetUsers`, `AspNetRoles`, `AspNetUserRoles`, `AspNetUserTokens`, … |

In SSMS:

```sql
USE CbeMeetingAttendance;
SELECT * FROM Employees;
SELECT * FROM Attendances;
SELECT Id, UserName, Email, EmployeeId, AccessFailedCount, LockoutEnd FROM AspNetUsers;
SELECT * FROM AspNetRoles;
SELECT * FROM AspNetUserRoles;
```

---

## Complete testing guide (start from beginning)

Follow these steps **in order**. Do not skip Authorize after login.

### Preparation

1. Start SQL Server Express (`.\SQLEXPRESS01`).
2. Open terminal in the project folder:

```powershell
cd C:\Users\LEUL\Desktop\dotnet\CbeMeetingAttendance
dotnet run
```

3. Wait until you see: `Now listening on: http://localhost:5151`
4. Open browser: **http://localhost:5151/swagger**

**Password rule for users:** at least 6 characters, must include **uppercase** and **lowercase**.  
Example that works: `Passw0rd`

---

### PART A — Employee + Attendance

#### Step A1 — Register employee

1. Find **`POST /api/employees/register`**
2. Click **Try it out**
3. Paste this body exactly:

```json
{
  "employeeId": "EMP-DEMO01",
  "fullName": "Sara Alemu",
  "division": "Head Office",
  "jobTitle": "Officer",
  "department": "HR",
  "phoneNumber": "0911223344"
}
```

4. Click **Execute**
5. Confirm status **200** and `"success": true`
6. In the response, copy the Guid field named **`id`**  
   Example: `"id": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"`  
   Keep this Guid. You need it for user registration.

#### Step A2 — Get all employees

1. Find **`GET /api/employees`**
2. Click **Try it out** → **Execute**
3. Confirm you see `EMP-DEMO01` in the list

#### Step A3 — Get employee by Guid

1. Find **`GET /api/employee/{id}`** (singular **employee**)
2. Paste the Guid from Step A1 into `id`
3. Click **Execute**
4. Confirm status **200**

#### Step A4 — Check employee (attendance status)

1. Find **`GET /api/employees/{employeeId}`** (plural **employees**)
2. Enter string code: `EMP-DEMO01` (not the Guid)
3. Click **Execute**
4. Confirm `"isAttended": false` (before taking attendance)

#### Step A5 — Take attendance

1. Find **`POST /api/attendance`**
2. Paste:

```json
{
  "employeeId": "EMP-DEMO01"
}
```

3. Click **Execute**
4. Confirm `"success": true`

#### Step A6 — Check attendance again

1. Find **`GET /api/attendance/check`**
2. Set query `employeeId` = `EMP-DEMO01`
3. Leave date and session empty
4. Click **Execute**
5. Confirm `"isAttended": true`

#### Step A7 — Dashboard + export

1. **`GET /api/admin/dashboard`** → Execute → confirm counts
2. **`GET /api/admin/export/excel`**
   - `format` = `excel` → Execute → Download
   - `format` = `csv` → Execute → Download
   - `format` = `pdf` → Execute → Download

---

### PART B — User management (Identity + JWT)

#### Step B1 — Register user

1. Find **`POST /api/users/register`**
2. Paste this body, but replace `employeeId` with the **Guid from Step A1**:

```json
{
  "email": "sara@cbe.et",
  "userName": "sara",
  "password": "Passw0rd",
  "employeeId": "PASTE-EMPLOYEE-GUID-FROM-STEP-A1"
}
```

3. Click **Execute**
4. Confirm status **200** and `"success": true`
5. Copy the user Guid from response field **`data.id`**  
   Keep it for roles and password steps.

#### Step B2 — Login

1. Find **`POST /api/users/login`**
2. Paste:

```json
{
  "emailOrUserName": "sara",
  "password": "Passw0rd"
}
```

3. Click **Execute**
4. Confirm status **200**
5. From `data`, copy these three values into a notepad:
   - **`token`** (long string starting with `eyJ...`)
   - **`refreshToken`**
   - **`user.userId`** (same as user Guid)

Also confirm `data.user.employee` is not null (employee info loaded).

#### Step B3 — Authorize Swagger (required to avoid 401)

1. At the **top right** of the Swagger page, click the green **Authorize** button (lock icon)
2. In the value box, paste **only** the full header value: `Bearer <token>` from Step B2  
   - Correct: `Bearer eyJ...`  
   - Wrong: do not paste `refreshToken`  
   - Wrong: do not paste only the raw token without the `Bearer ` prefix
3. Click **Authorize**
4. Click **Close**
5. The lock should now look locked/authorized

If you skip this step, `GET /api/users` returns **401 Unauthorized**.

#### Step B4 — Get all users

1. Find **`GET /api/users`**
2. Click **Try it out** → **Execute**
3. Confirm status **200** (not 401)
4. Confirm `"success": true` and `"total"` is at least 1

#### Step B5 — Get user by Id

1. Find **`GET /api/users/{id}`**
2. Paste `user.userId` from Step B2
3. Click **Execute**
4. Confirm status **200** and employee info is included

#### Step B6 — Assign role

1. Find **`POST /api/users/assign-role`**
2. Paste (use your real user Guid):

```json
{
  "userId": "PASTE-USER-GUID",
  "roleName": "Admin"
}
```

3. Click **Execute**
4. Confirm `"success": true`

#### Step B7 — Change role

1. Find **`POST /api/users/change-role`**
2. Paste:

```json
{
  "userId": "PASTE-USER-GUID",
  "currentRole": "Admin",
  "newRole": "Staff"
}
```

3. Click **Execute**
4. Confirm `"success": true`

#### Step B8 — Remove role

1. Find **`POST /api/users/remove-role`**
2. Paste:

```json
{
  "userId": "PASTE-USER-GUID",
  "roleName": "Staff"
}
```

3. Click **Execute**
4. Confirm `"success": true`

#### Step B9 — Change password

1. Find **`POST /api/users/change-password`**
2. Paste:

```json
{
  "userId": "PASTE-USER-GUID",
  "currentPassword": "Passw0rd",
  "newPassword": "NewPass1"
}
```

3. Click **Execute**
4. Confirm `"success": true`
5. Login again with:

```json
{
  "emailOrUserName": "sara",
  "password": "NewPass1"
}
```

6. Copy the **new token**, open **Authorize**, clear old token if needed, paste the new token, Authorize again

#### Step B10 — Reset password

1. Find **`POST /api/users/reset-password`**
2. Paste:

```json
{
  "userId": "PASTE-USER-GUID",
  "newPassword": "ResetPass1"
}
```

3. Click **Execute**
4. Confirm `"success": true`
5. You can now login with password `ResetPass1`

#### Step B11 — Refresh token

1. Find **`POST /api/users/refresh`**
2. Paste the `refreshToken` you saved from login (use a login that happened **after** any logout, or login again first and copy a fresh refreshToken):

```json
{
  "refreshToken": "PASTE-REFRESH-TOKEN"
}
```

3. Click **Execute**
4. Confirm you get a new `token` and `refreshToken`
5. Update Swagger Authorize with the new `token`

#### Step B12 — Logout

1. Find **`POST /api/users/logout`**
2. Make sure Authorize still has a valid token
3. Click **Try it out** → **Execute** (no body)
4. Confirm `"success": true` and message `Logout successful.`
5. Optional: call refresh with the old refreshToken → should fail

---

## Order checklist (print this)

| Order | Endpoint | Copy what? |
|------:|----------|------------|
| 1 | `POST /api/employees/register` | Copy employee **Guid `id`** |
| 2 | `GET /api/employees` | — |
| 3 | `GET /api/employee/{guid}` | — |
| 4 | `GET /api/employees/EMP-DEMO01` | — |
| 5 | `POST /api/attendance` | — |
| 6 | `GET /api/attendance/check` | — |
| 7 | `GET /api/admin/dashboard` | — |
| 8 | `GET /api/admin/export/excel` | — |
| 9 | `POST /api/users/register` | Copy user **Guid `id`** |
| 10 | `POST /api/users/login` | Copy **token**, **refreshToken**, **userId** |
| 11 | Swagger **Authorize** | Paste full `Bearer <token>` value |
| 12 | `GET /api/users` | Must be 200, not 401 |
| 13 | `GET /api/users/{id}` | — |
| 14 | `POST /api/users/assign-role` | — |
| 15 | `POST /api/users/change-role` | — |
| 16 | `POST /api/users/remove-role` | — |
| 17 | `POST /api/users/change-password` | Then login again + re-Authorize |
| 18 | `POST /api/users/reset-password` | — |
| 19 | `POST /api/users/refresh` | Update Authorize with new token |
| 20 | `POST /api/users/logout` | — |

---

## Troubleshooting

| Problem | Exact cause | Exact fix |
|---------|-------------|-----------|
| **401 Unauthorized** on `/api/users` | No JWT, bad JWT, or expired JWT | Login → copy `token` → Swagger **Authorize** → try again |
| Token expired | JWT lasts only 15 minutes | Login again and re-Authorize |
| User register: Employee not found | You used string `EMP-DEMO01` instead of Guid | Use employee Guid `id` from register employee response |
| Password validation failed | Missing upper or lower case, or shorter than 6 | Use e.g. `Passw0rd` |
| Port 5151 in use | Another process is running | `netstat -ano \| findstr :5151` then `Stop-Process -Id <PID> -Force` |
| SQL connection error | SQL Server not running | Start SQL Server Express service |
| 404 on `/api/employees/{guid}` | Wrong route | Guid → `/api/employee/{id}` ; code → `/api/employees/{employeeId}` |

---

## NuGet packages

| Package | Purpose |
|---------|---------|
| `Microsoft.EntityFrameworkCore.SqlServer` | SQL Server |
| `Microsoft.AspNetCore.Identity.EntityFrameworkCore` | Identity users/roles/password/lockout |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | Validate JWT on protected endpoints |
| `System.IdentityModel.Tokens.Jwt` | Create JWT on login/refresh |
| `Microsoft.AspNetCore.OpenApi` | OpenAPI document |
| `NSwag.AspNetCore` | Swagger UI + Authorize button |
| `EPPlus` | Excel export |
| `PDFsharp` | PDF export |


#   C b e M e e t i n g A t t e n d a n c e B a c k e n d  
 