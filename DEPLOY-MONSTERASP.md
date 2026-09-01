# 🚀 MonsterASP.NET — Free Deployment Guide

Yeh guide aapke HelpDesk ko **MonsterASP.NET ke free plan** pe deploy karti hai.
Cost: **$0/month, no credit card.**

## Free plan me kya milta hai
- 1 website (free subdomain, e.g. `yourapp.monsterasp.net`)
- 5 GB storage, 256 MB dedicated RAM
- **1 SQL Server database (1 GB)** — auto-migrate+seed startup pe khud ho jata hai
- .NET 8 + SignalR/WebSockets supported
- Limits: limited traffic, no custom domain, no daily backups

## Architecture (single app — SPA + API ek saath)
```
https://yourapp.monsterasp.net
        │  (IIS - ASP.NET Core 8)
        ├── /            → React SPA (wwwroot/index.html)
        ├── /api/...     → REST API
        ├── /hubs/tickets→ SignalR (WebSockets)
        ├── /uploads/... → attachments (PhysicalFileProvider)
        └── /health      → {"status":"Healthy"}
```
Frontend publish ke waqt automatically build ho kar API ke `wwwroot` me copy hota hai
(csproj me `BuildSpa` target). DB schema bhi startup pe `DbSeeder` khud migrate karta hai.

---

## Step 1 — Signup (5 min, no card)
1. https://www.monsterasp.net → **Try for FREE**
2. Email + password → account confirm
3. Control panel khul jayega

## Step 2 — SQL Server database banao
1. Control panel → **Databases** → **Create database** (SQL Server)
2. DB name, username, password note karo
3. Connection details (server host) copy karo

## Step 3 — Production settings bharna
File kholo: `src/HelpDesk.Api/appsettings.Production.json`
> Ye file git me **nahi** hai (password safety ke liye). Naye machine pe
> `appsettings.Production.template.json` ko copy karke naam `appsettings.Production.json` rakho.

```json
{
  "ConnectionStrings": {
    "HelpDeskDb": "Server=<DB_HOST>;Database=<DB_NAME>;User ID=<DB_USER>;Password=<DB_PASS>;TrustServerCertificate=True;MultipleActiveResultSets=true;"
  },
  "Jwt": {
    "Secret": "<koi-32+-characters-lamba-random-secret>"
  },
  "Seed": {
    "AdminPassword": "<admin@helpdesk.local ka password>"
  }
}
```
> `TrustServerCertificate=True` zaroor rakho — shared hosts ka TLS cert chain client ke trust store me nahi hota.

## Step 4 — Publish karo (1 command)
Repo root se:
```powershell
dotnet publish src/HelpDesk.Api/HelpDesk.Api.csproj -c Release -r win-x64 --self-contained false -o publish
```
- Ye React build + API build dono karega
- `-r win-x64` → sirf Windows ke native files include hote hain (`runtimes/` folder ka ~30 MB junk skip)
- Output `publish/` folder me ready hoga (`web.config` auto-generated included)
- **FTP pe sirf `publish/` folder ka content upload karo — repo ka aur kuch nahi**

## Step 5 — FTP se upload karo
1. Control panel → **FTP accounts / credentials** copy karo
2. FileZilla / WinSCP se `publish/` ke **saare files** site ke root me upload karo
   (ya WinSCP CLI: `winscp.com /command "open ftp://user:pass@host/" "put -r C:\new1\publish\* /" "exit"`)

## Step 6 — Live 🎉
- Site kholo: `https://<yourapp>.monsterasp.net`
- Login: `admin@helpdesk.local` / (Step 3 wala password)
- Test: ticket banao, message bhejo, dusre browser me agent login karke real-time reply
  (SignalR check), file upload karo.

---

## Update deploy karna (dobara)
```powershell
dotnet publish src/HelpDesk.Api/HelpDesk.Api.csproj -c Release -r win-x64 --self-contained false -o publish
# FTP se publish/* dobara upload (overwrite) karo
```

## Troubleshooting
| Problem | Fix |
|---|---|
| 500.30 / app crash on start | Connection string galat hai — `appsettings.Production.json` check karo (uploaded file edit karke FTP se wapas upload kar sakte ho) |
| Login fail | `Seed:AdminPassword` Step 3 me set karo, phir DB delete+recreate (naya seed) ya manual password reset |
| Blank page / assets 404 | `wwwroot/index.html` upload hua hai? Publish output me check karo |
| Realtime nahi chal raha | WebSockets enable hai? MonsterASP pe supported hai — browser dev tools → Network → WS check karo |
| DB migration error | DB user ko `db_owner` role chahiye (hosting panel me check karo) |

## Notes
- Uploads folder (`uploads/`) app ke writable folder me banta hai — shared hosting me app pool
  identity ke paas write access hota hai; agar nahi hota to hosting panel se folder permission `write` karo.
- HTTPS free plan me custom domain ke bina listed nahi hai; subdomain URL browser me warning de
  to bhi kaam karega (same-origin HTTP), par production use ke liye Premium (Let's Encrypt) behtar.
- AI reply rule-based fallback chalega (koi API key nahi di to). Key deni ho to `Ai:ApiKey` add karo
  `appsettings.Production.json` me.
