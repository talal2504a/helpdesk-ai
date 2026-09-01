# HelpDesk — Project Plan & Architecture Reference

Yeh doc project ka "single source of truth" hai: kya banaya gaya hai, kaun si file kya karta hai, kaunse connection/env vars kahan use hote hain, seed kya hai, aur aage ka plan.

## 1. Current state

| Item | Status |
|---|---|
| Solution `HelpDesk.sln` (5 projects) | ✅ |
| Entities + EF Core mapping + migrations | ✅ auto-migrate on startup |
| JWT auth + PBKDF2 password hashing | ✅ |
| RBAC (Customer / Agent / Admin) | ✅ |
| Ticket CRUD + search + pagination + filters | ✅ |
| Messages + attachments | ✅ |
| TicketHistory (audit trail) | ✅ |
| SignalR hub (live messages/notifications) | ✅ |
| AI triage + rule-based fallback | ✅ |
| Dashboard statistics | ✅ |
| React frontend | ✅ (npm build passes) |
| xUnit tests | ✅ 8 passing |
| Docker / docker-compose | ✅ files present |
| Azure / GitHub-Actions readiness | ✅ present |
| README / PLAN | ✅ here |

## 2. Architecture overview

```
React (Vite, :3000/:5173) → nginx/proxy → ASP.NET Core 8 (:8080/:5080)
        │                                    ├─ Controllers (REST /api/*)
        │                                    ├─ Hubs/TicketHub (SignalR /hubs/tickets)
        │                                    └─ Middleware/ExceptionHandlingMiddleware
        └─ @microsoft/signalr ───────────────► /hubs/tickets (JWT via query string)
```

```
HelpDesk.Api ─► HelpDesk.Infrastructure ─► HelpDesk.Application ─► HelpDesk.Domain
   (startup/controllers/hub/swagger)  (EF/repos/services/AI/JWT)  (DTOs/interfaces)  (entities/enums)
```

## 3. Folder/file map (kia karta hai)

### Domain `src/HelpDesk.Domain`
- `Entities/*` — User, Department, Ticket, Category, Priority, Status, Message, Attachment, TicketHistory, AIAnalysis.
- `Enums/UserRole.cs` — roles + `ToRoleName()`.

### Application `src/HelpDesk.Application`
- `DTOs/*` — request/response records (entities never exposed directly).
- `Interfaces/*` — service contracts (IAuthService, ITicketService, INotifier, IAiClient, IFileStorage, ICurrentUser…).
- `Validators/*`, `Exceptions/AppException.cs` — validation + typed errors.

### Infrastructure `src/HelpDesk.Infrastructure`
- `Data/HelpDeskDbContext.cs` — configs/indexes/FKs/delete behaviors.
- `Data/Migrations/*` — EF Core `InitialCreate`.
- `Data/DbSeeder.cs` — seed lookups + demo users.
- `Repositories/` — GenericRepository + TicketRepository.
- `Services/` — Auth, User, Ticket, Message, Attachment, Admin, Dashboard, AiAnalysis (single source of truth).
- `Ai/` — OpenAI client + rule-based fallback.
- `Security/` — PBKDF2 hasher, JWT token service.
- `RealTime/SignalRNotifier.cs`, `Storage/LocalFileStorage.cs`.
### API (`src/HelpDesk.Api`)
- Controllers: Auth, Users, Tickets, Lookup, Admin, Dashboard, Ai.
- `Hubs/TicketHub.cs` — groups `ticket:{id}` + `user:{id}`, `JoinTicket`, `OnConnected`.
- `Middleware/ExceptionHandlingMiddleware.cs` — uniform `{error:{code,message[,errors]}}`.
- `Common/HttpCurrentUser.cs` — JWT claims → `ICurrentUser`.
- `Program.cs` — DI, JWT, Swagger, CORS, hub map, startup migrate + seed.
- `Properties/launchSettings.json` — F5 profile on :5080.

### Frontend (`frontend/`)
- `src/api.js` — Axios + auth interceptor + SignalR emitter.
- `src/App.jsx` — routing + role-guard.
- `src/components/Layout.jsx` — navbar, broadcast bell notifications (SignalR hub events → bell dropdown).
- `src/pages/*` — Login, Register, MyTickets, TicketDetail (chat+AI+history), CreateTicket, AgentQueue, Dashboard, AdminUsers, AdminLookups.
- `vite.config.js` — dev proxy `/api`, `/hubs` (ws), `/uploads`.

## 4. Environment variables & secrets (connections)

Kabhi code me hardcode nahi. Dev = user-secrets (`:`), prod/docker = env (`__`).

| Var (prod) | Dev (user-secrets `:`) | Purpose |
|---|---|---|
| `ConnectionStrings__HelpDeskDb` | `ConnectionStrings:HelpDeskDb` | SQL Server connection |
| `Jwt__Secret` | `Jwt:Secret` | ≥32 char signing key |
| `Jwt__Issuer`/`Audience`/`ExpiryMinutes` | same colons | JWT tuning |
| `Seed__AdminPassword` | `Seed:AdminPassword` | demo user password |
| `Ai__ApiKey`/`BaseUrl`/`Model` | same colons | AI provider (omit = fallback) |
| `Storage__AttachmentsPath` | colons | upload dir |
| `Cors__Origins` | colons | allowed origins |

Dev LocalDB connection string:
```
Server=(localdb)\MSSQLLocalDB;Database=HelpDeskDb;Trusted_Connection=True;MultipleActiveResultSets=true
```

## 5. Database (tables + FKs)

Users, Departments, Tickets, Categories, Priorities, Statuses, Messages, Attachments, TicketHistory, AiAnalyses.
- Ticket→Status/Priority/Category (Restrict); Department/Agent (SetNull); User (Restrict).
- Message→Ticket (Cascade), Sender (Restrict). Attachment→Ticket (Cascade), Message (NoAction), Uploader (Restrict).
- TicketHistory→Ticket (Cascade), User (Restrict); AIAnalysis→Ticket (Cascade).
- `TicketNumber` unique `TKT-yyyyMMdd-NNNN`.

## 6. Aage ka plan (future roadmap)

1. Refresh tokens (abhi sirf access token).
2. `IFileStorage` → Azure Blob implementation.
3. Email/SMS (SendGrid) optional with realtime.
4. Background worker: SLA breach alerts, stale-ticket AI re-scan.
5. Public ticket-status widget by number (no auth).
6. Rate limiting + Serilog/App Insights logging.
7. Health checks with DB deps (`AddHealthChecks`), ACA/K8s probes.
8. UI state store + optimistic updates on SignalR.
9. Multi-tenant scoping (orgId).
10. GraphQL (HotChocolate) reusing REST services.
11. **Deployment**: Docker, IIS/shared hosting, and GitHub Pages-style static deployment notes are maintained in the repo docs.

## 7. How to run (quick)

```bash
# Backend (LocalDB)
cd src/HelpDesk.Api
dotnet user-secrets set "ConnectionStrings:HelpDeskDb" "Server=(localdb)\MSSQLLocalDB;Database=HelpDeskDb;Trusted_Connection=True;MultipleActiveResultSets=true"
dotnet user-secrets set "Jwt:Secret" "a-32-char-min-secret"
dotnet run --urls http://localhost:5080   # Swagger /api

# Frontend
cd frontend && npm install && npm run dev     # :5173

# Docker
cp .env.example .env   # fill SA_PASSWORD, JWT_SECRET, SEED_ADMIN_PASSWORD
docker compose up -d   # frontend:3000  api:8080  sql:1433
```