@echo off
echo ============================================
echo  Help Desk - Auto Setup and Run
echo ============================================
echo.

cd C:\new1\src\HelpDesk.Api

echo [1/3] Setting up user secrets...
dotnet user-secrets set "ConnectionStrings:HelpDeskDb" "Server=(localdb)\\SSQLLocalDB;Database=HelpDeskDb;Trusted_Connection=True;MultipleActiveResultSets=true"
dotnet user-secrets set "Jwt:Secret" "at-least-32-characters-long-secret-change-me-1234567890"
dotnet user-secrets set "Seed:AdminPassword" "HelpDesk@123"

echo.
echo [2/3] Building project...
dotnet build

echo.
echo [3/3] Starting application...
echo.
echo ============================================
echo  Application starting...
echo  URL: https://localhost:5001
echo  Swagger: https://localhost:5001/swagger
echo ============================================
echo.

dotnet run
