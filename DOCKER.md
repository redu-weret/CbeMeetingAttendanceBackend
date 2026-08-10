# Docker Deployment — CBE Meeting Attendance

This project runs as three containers via Docker Compose:

```
┌──────────────┐   http://localhost:8080   ┌──────────────┐   /api + /Profiles   ┌──────────────┐   TCP 1433   ┌──────────────┐
│  React SPA   │ ─────────────────────────► │    Nginx     │ ──────────────────► │  ASP.NET API │ ──────────► │  SQL Server  │
│ (nginx:alpine)│                           │ (web service) │                    │ (api service) │             │   (db)       │
└──────────────┘   http://localhost:5151    └──────────────┘   (internal net)    └──────────────┘              └──────────────┘
                        (direct API + Swagger)
```

| Service | Container image | Host port | Container port | Notes |
|---------|-----------------|-----------|----------------|-------|
| `web`   | `cbemeetingattendance-web`  | **8080** | 80 | Nginx serves the built React app and proxies `/api` + `/Profiles` to the API |
| `api`   | `cbemeetingattendance-api`  | **5151** | 80 | ASP.NET Core (.NET 10) Web API; Swagger at `http://localhost:5151/swagger` |
| `db`    | `mssql/server:2022-latest`  | *(none published)* | 1433 | SQL Server; reachable by the API as `db:1433` |

The `db` port is deliberately **not published** to the host so it cannot clash with a local
SQL Server Express instance already using port 1433. To query the container DB:

```bash
docker compose exec db /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" -C -Q "SELECT name FROM sys.databases"
```

---

## Folder layout (required)

The compose file uses a build context that points at the React project **as a sibling folder**:

```
Desktop/
├── CbeMeetingAttendance/          # ASP.NET backend (this folder, contains docker-compose.yml)
└── React/
    └── CbeMeetingAttendance/      # React + Vite frontend (has its own Dockerfile + nginx.conf)
```

If you move/rename either folder, update the `web.build.context` line in `docker-compose.yml`
(currently `../React/CbeMeetingAttendance`).

---

## Environment variables

| Variable | File | Purpose |
|----------|------|---------|
| `SA_PASSWORD` | `.env` (gitignored) | SQL Server `sa` password; used by `db` and injected into the API connection string |

The API container overrides the local `appsettings.json` connection string (`.\SQLEXPRESS`,
used by local `dotnet run`) with a Docker connection string pointing at the `db` service —
**config only, no code changed**. Other settings (JWT key, AdminUser, audiences) are read
from `appsettings.json` exactly as outside Docker, so tokens and behavior are identical.

Optional overrides for deployment (compose `api.environment`):
`JwtSettings__Key`, `JwtSettings__Issuer`, `AdminUser__UserName`, `AdminUser__Email`, `AdminUser__Password`.

---

## Commands

Run everything (build + start, detached):

```bash
cd C:\Users\abcd\OneDrive\Desktop\CbeMeetingAttendance
docker compose up -d --build
```

Build only:

```bash
docker compose build
```

Watch logs:

```bash
docker compose logs -f api        # or web, or db
```

Stop (containers stay; data volumes persist):

```bash
docker compose down
```

Stop and delete the data volumes (**destroys container data only** — local SQL Express
`.\SQLEXPRESS` and its `CbeMeetingAttendance_New` database are never touched):

```bash
docker compose down -v
```

Rebuild after source changes:

```bash
docker compose up -d --build --force-recreate
```

Remove containers and images:

```bash
docker compose down --rmi all
docker rmi cbemeetingattendance-api cbemeetingattendance-web
```

Remove everything, including volumes and the anonymous network:

```bash
docker compose down -v --rmi all
```

---

## URLs after startup

| What | URL |
|------|-----|
| React app | http://localhost:8080 |
| API (direct) | http://localhost:5151 |
| Swagger UI | http://localhost:5151/swagger |
| OpenAPI JSON | http://localhost:5151/swagger/v1/swagger.json |
| Uploaded profile pictures | http://localhost:8080/Profiles/<file> (proxied) |

On first startup the API runs EF migrations and seeds the admin account from
`AdminUser` in `appsettings.json` (admin@gmail.com / Admin@11) **only if the database has
no users** — same behavior as local `dotnet run`.

---

## Quick verification (Docker)

```bash
# 1. Swagger loads
curl -o /dev/null -s -w "%{http_code}\n" http://localhost:5151/swagger/index.html     # 200

# 2. Login (JWT) — through the Nginx frontend
curl -s -X POST http://localhost:8080/api/users/login ^
  -H "Content-Type: application/json" ^
  -d "{\"emailOrUserName\":\"admin@gmail.com\",\"password\":\"Admin@11\"}"

# 3. Admin-only endpoint with the returned token (authorization works)
curl -s http://localhost:5151/api/users -H "Authorization: Bearer <TOKEN>"

# 4. React SPA + deep link fallback
curl -o /dev/null -s -w "%{http_code}\n" http://localhost:8080/          # 200
curl -o /dev/null -s -w "%{http_code}\n" http://localhost:8080/login     # 200 (SPA fallback)
```

Full feature-by-feature test: follow the API flow in the project README, but use
`http://localhost:5151` (Swagger) and `http://localhost:8080` (app) instead of local URLs.

---

## Notes & troubleshooting

- **Port 5151 conflict:** if your local `dotnet run` API is already listening on 5151, stop it
  before `docker compose up` (or change the `api.ports` mapping).
- **First boot:** `db` initializes its data volume (~1–2 min) before `api` starts; the API
  entrypoint additionally waits for SQL Server, and `restart: on-failure` recovers from any
  transient SQL connection drop (the cause of the original crash). `web` starts only after
  `api` is healthy.
- **PDF export:** the image installs Liberation Sans and aliases it as `arial*.ttf` in `/app`
  so PDFsharp's `WindowsFontResolver` works on Linux. PDF export was verified end-to-end.
- **Profile pictures** persist in the `profiles-data` volume (mounted at `/app/wwwroot/Profiles`).
- **Data protection keys** for ASP.NET Identity data-protection are stored under
  `/root/.aspnet/DataProtection-Keys` — persisted by the container filesystem (not a volume).
  For production, mount a volume there or configure a key store.
- **JWT key `"cbe"` and the admin password are the local defaults** — kept identical to
  preserve behavior; override via the environment variables above before any shared deployment.
