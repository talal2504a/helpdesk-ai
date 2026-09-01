Set-Location C:\new1\src\HelpDesk.Api
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ASPNETCORE_URLS = "http://localhost:5080"
dotnet run --no-launch-profile
