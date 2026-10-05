# HelpDesk — Customer Support Ticketing System

HelpDesk is a production-style customer support / ticketing platform built with **ASP.NET Core 8 Web API** and a **React (Vite)** frontend.

Live site: https://helpdesk.tryasp.net/login

It is designed for:
- customer support teams
- internal help desks
- IT service desks
- small businesses that need structured ticket handling

The app supports **role-based access**, **real-time ticket updates**, **attachments**, **ticket history**, **dashboard stats**, and **AI-assisted ticket triage**.

## What it does

- Customers can register, login, and create support tickets
- Customers can reply to their own tickets and upload attachments
- Agents can view assigned/unassigned tickets, respond, and manage queue work
- Admins can manage users and system lookup data
- AI can classify tickets, suggest replies, and assist support workflows
- Real-time updates keep messages and notifications in sync

## Roles

### Customer
- Register and login
- Create new support tickets
- View only their own tickets
- Reply in ticket conversations
- Upload attachments

### Agent
- View support queue and assigned tickets
- Reply to customer messages
- Change ticket status / priority where allowed
- Take over conversations when needed
- Work with real-time notifications

### Admin
- Manage users
- Create agent accounts
- Manage departments, categories, priorities, and statuses
- Access reporting / dashboard data
- Remove or maintain system data

## Tech stack

| Layer | Tech |
|---|---|
| Backend | ASP.NET Core 8 Web API |
| Data | SQL Server + Entity Framework Core 8 |
| Auth | JWT + PBKDF2 password hashing |
| Realtime | SignalR + HTTP polling fallback |
| AI | OpenAI-compatible client + rule-based fallback |
| Frontend | React 18, Vite, Axios, React Router |
| Styling | Tailwind CSS + custom utility classes |
| Testing | xUnit |

## Architecture

```text
React (Vite)
   ↓
ASP.NET Core API
   ├── Controllers (REST)
   ├── SignalR Hub (realtime updates)
   ├── Services / Repositories
   └── EF Core / SQL Server
```

## Main folders

- `src/HelpDesk.Domain` — entities and enums
- `src/HelpDesk.Application` — DTOs, interfaces, validators, exceptions
- `src/HelpDesk.Infrastructure` — EF Core, repositories, services, AI, storage, realtime
- `src/HelpDesk.Api` — controllers, hub, middleware, startup config
- `frontend` — React client
- `tests/HelpDesk.Tests` — xUnit tests

## Key features

- JWT authentication
- Role-based authorization
- Ticket lifecycle management
- Conversation history / audit trail
- File attachments
- Search, filters, pagination
- Dashboard metrics
- AI classification / suggested replies
- Real-time notifications and updates
- Responsive UI for mobile and desktop

## Demo credentials

Use these demo users for local testing:

| Role | Email | Password |
|---|---|---|
| Admin | `admin@helpdesk.local` | `HelpDesk@123` |
| Agent | `agent@helpdesk.local` | `HelpDesk@2026!Agent` |
| Customer | `customer@helpdesk.local` | `HelpDesk@2026!Customer` |

> If your environment uses different seed passwords, update the values in your production seed/configuration.

## Running locally

### Backend

```powershell
cd src/HelpDesk.Api
dotnet user-secrets set "ConnectionStrings:HelpDeskDb" "Server=(localdb)\MSSQLLocalDB;Database=HelpDeskDb;Trusted_Connection=True;MultipleActiveResultSets=true"
dotnet user-secrets set "Jwt:Secret" "a-32-char-min-secret"
dotnet run --urls http://localhost:5080
```

### Frontend

```powershell
cd frontend
npm install
npm run dev
```

Frontend dev server: `http://localhost:3000` or `http://localhost:5173` depending on the active config.

### Docker

```powershell
cp .env.example .env
docker compose up -d
```

## Production / deployment notes

- Keep secrets out of source control
- Use environment variables in production
- Upload the compiled frontend/API publish output to the server
- Keep `robots.txt` and `sitemap.xml` in the public frontend output for SEO

## SEO / public indexing

The app includes basic SEO files for the public site:
- `robots.txt`
- `sitemap.xml`
- verification meta tag support in `frontend/index.html`

## Testing

```powershell
cd tests/HelpDesk.Tests
dotnet test
```

## Notes

- The app is designed with a clean layered architecture.
- Real-time support falls back to HTTP polling if SignalR/WebSockets are limited by the host.
- Generated folders like `bin`, `obj`, `dist`, `publish`, and `node_modules` should not be committed.
