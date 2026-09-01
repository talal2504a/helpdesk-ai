@echo off
echo Starting HelpDesk Frontend...
cd /d C:\new1\frontend
if not exist node_modules (
    echo Installing dependencies...
    call npm install
)
echo Starting frontend on http://localhost:3000
npm run dev
