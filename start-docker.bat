@echo off
echo ============================================
echo  Help Desk - Docker Auto Start
echo ============================================
echo.
echo Starting backend + frontend + database...
echo.

cd C:\new1

echo [1/2] Building and starting Docker containers...
docker-compose up --build -d

echo.
echo [2/2] Waiting for services to start...
timeout /t 5 /nobreak >nul

echo.
echo ============================================
echo  Services Started!
echo ============================================
echo Frontend:  http://localhost:3000
echo Backend:   http://localhost:8080
echo Database:  localhost:1433
echo.
echo Demo Credentials:
echo   Admin:    admin@helpdesk.local / HelpDesk@123
echo   Agent:    agent@helpdesk.local / HelpDesk@123
echo   Customer: customer@helpdesk.local / HelpDesk@123
echo.
echo Opening frontend in browser...
start http://localhost:3000

echo.
echo To stop: docker-compose down
echo ============================================
pause
