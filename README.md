# Project Status Tracker — real version

A web app to replace Excel for project status tracking. Progress is calculated
from completed subtasks — never typed in — and the weekly/fortnightly report
and PowerPoint are drafted automatically from live data.

## Stack

- **Backend:** ASP.NET Core 8 Web API, Entity Framework Core + SQLite, JWT auth
  (BCrypt password hashes), ClosedXML for `.xlsx`, DocumentFormat.OpenXml for
  `.pptx`, minimal PDF writer for `.pdf`
- **Frontend:** React 19 + Vite + TypeScript, calling the API (Vite proxies
  `/api` to `http://localhost:5000` in development)

## Run it

```bash
# Server (from the repo root)
cd server
~/workspace/.dotnet/dotnet run          # or: dotnet run
# API on http://localhost:5000, Swagger at /swagger

# Client (new terminal, from the repo root)
cd client
npm install
npm run dev                              # http://localhost:5173
```

On first start the server creates `app.db` (SQLite) and seeds the demo users
and the sample project below. No external database or server is needed.

## Seed logins (demo only)

All seed accounts use password **`ChangeMe123!`** — change it before any real
use (there is no password-change screen yet; see "Not yet done").

| Email | Name | Role |
|---|---|---|
| president@demo.local | Ada President | President |
| evp@demo.local | Eva Executive | EVP |
| vp@demo.local | Victor Vice | VP |
| director@demo.local | Daniel Okafor | Director |
| manager@demo.local | Maya Chen | Manager |
| scrum@demo.local | Sam Mwangi | ScrumMaster |
| dev@demo.local | Priya Nair | Developer |
| dev2@demo.local | Alex Rivera | Developer |

## Role permissions (enforced in the API, not just the UI)

| Role | Can do |
|---|---|
| President, EVP, VP, Director | View-only: read projects, dashboard, reports, downloads |
| Manager | Owns projects, creates projects, edits tasks/subtasks in their own projects, generates reports, reviews/approves reports |
| ScrumMaster | Creates/updates tasks, updates subtask status and blocker notes, generates reports |
| Developer | Updates the status of their own subtasks only |

## How progress and health work

- Task progress = completed subtasks / total subtasks; project progress rolls that up.
- Health: **Delayed** if any subtask is blocked or overdue (due before today and
  not done); **AtRisk** if work is due within 3 days and still in progress;
  otherwise **OnTrack**.

## Sample data (seeded)

**Customer Portal Redesign** — manager Maya Chen, weekly, target 2026-12-18.
3 tasks, 10 subtasks, 3 done = **30%**.

- Design new onboarding flow (50%): User research interviews (done), Wireframes
  for signup (done), Final UI mockups (in progress), Design review with manager
  (not started)
- Build login and account pages (25%): Set up auth API (done), Build login page
  (in progress), Build account settings page (not started), Fix password reset
  bug (**blocked**, due 2026-11-05, "Waiting for security review")
- Migrate customer data (0%): Clean old customer records (in progress),
  Test migration script (not started)

## API

All endpoints except `POST /api/auth/login` require a JWT (`Authorization: Bearer <token>`).

| Method & path | Purpose |
|---|---|
| POST /api/auth/login | Email + password → JWT |
| POST /api/auth/register | Seed/admin only, Manager JWT required — **not exposed in the UI** |
| GET /api/dashboard | Portfolio progress, needs attention (blocked + overdue) |
| GET /api/projects, POST /api/projects, GET /api/projects/{id} | Projects (create: Manager only) |
| GET/POST /api/projects/{projectId}/tasks, PUT /api/tasks/{id} | Tasks (Manager/ScrumMaster) |
| GET/POST /api/tasks/{taskId}/subtasks, PUT /api/subtasks/{id} | Subtasks (status/blocker rules per role) |
| GET /api/mywork | Subtasks owned by the signed-in user |
| GET /api/users | Team members and roles |
| POST /api/reports/generate | Draft a report from live data (projectId optional, cadence, periodEnd); includes a 7-slide outline in JSON |
| GET /api/reports, GET /api/reports/{id} | Reports |
| PUT /api/reports/{id}/review | Mark reviewed (Manager only) |
| GET /api/reports/{id}/download?format=pdf\|ppt\|xlsx | Real file download: PDF, PPTX (gold/purple/black theme), XLSX |

## Configuration

- `server/appsettings.json` holds the connection string and a **dev-only** JWT
  key. Change `Jwt:Key` (at least 32 characters) before any real use.
- `server/appsettings.Development.json.example` shows the development override
  shape. Never commit a real `appsettings.Development.json` with secrets —
  it is git-ignored.

## Verified

- `dotnet build` in `server/` passes.
- `npm install && npm run build` in `client/` passes.
- Live run (2026-11-09): login as `manager@demo.local`, `GET /api/dashboard`
  returned 30% overall (3/10 subtasks) and the blocked subtask
  "Fix password reset bug" in Needs Attention. Report generation produced a
  7-slide deck; `.xlsx`, `.pptx` and `.pdf` downloads all returned 200 with
  valid file contents. Role checks verified: President updating a subtask →
  403, a Developer updating someone else's subtask → 403, updating their own
  → 200, no token → 401, wrong password → 401.

## Not yet done

- Real email / notifications and reminders (e.g. report due, overdue subtasks)
- Password change / reset, and a proper admin user-management screen
- Production database (currently SQLite file `app.db`, git-ignored) and deployment
  (hosting, HTTPS, environment secrets)
- Excel import (a CSV template exists in the earlier prototype work; import is
  not yet built into this API)
- Audit log of who changed what
