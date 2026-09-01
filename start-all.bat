@echo off
echo ============================================
echo  Help Desk - Complete Setup
echo ============================================
echo.

cd C:\new1

echo [Step 1/4] Setting up backend user secrets...
cd /d C:\new1\src\HelpDesk.Api
dotnet user-secrets set "ConnectionStrings:HelpDeskDb" "Server=(localdb)\\MSSQLLocalDB;Database=HelpDeskDb;Trusted_Connection=True;MultipleActiveResultSets=true" 2>nul
dotnet user-secrets set "Jwt:Secret" "at-least-32-characters-long-secret-change-me-1234567890" 2>nul
dotnet user-secrets set "Seed:AdminPassword" "HelpDesk@123" 2>nul
echo User secrets configured successfully.

echo.
echo [Step 2/4] Building project	
cd /d C:\new1
dotnet build src/HelpDesk.Api/HelpDesk.Api.csproj

echo.
echo [Step 3/4] Starting Backend API...
echo Backend will run on: http://localhost:5000
echo.
start "HelpDesk API" cmd /k "cd /d C:\new1\src\HelpDesk.Api && dotnet run --urls=http://localhost:5000"

echo Waiting for backend to start...
timeout /t 8 /nobreak >nul

echo.
echo [Step 4/4] Starting Frontend...
echo Frontend will run on: http://localhost:3000
echo.
start "HelpDesk Frontend" cmd /k "cd /d C:\new1\frontend && npm run dev"

echo.
echo ============================================
echo  Setup Complete!
echo ============================================
echo.
echo Backend API:  http://localhost:5000
echo Frontend:     http://localhost:3000
echo.
echo Demo Credentials:
echo   Admin:    admin@helpdesk.local / HelpDesk@123
echo   Agent:    agent@helpdesk.local / HelpDesk@123
echo   Customer: customer@helpdesk.local / HelpDesk@123
echo.
echo Opening browser in 5 seconds...
timeout /t 5 /nobreak >nul
start http://localhost:3000

echo.
echo Press any key to exit...
pause >nul
