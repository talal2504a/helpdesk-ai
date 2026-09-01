# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore layers for caching
COPY HelpDesk.sln ./
COPY src/HelpDesk.Domain/HelpDesk.Domain.csproj src/HelpDesk.Domain/
COPY src/HelpDesk.Application/HelpDesk.Application.csproj src/HelpDesk.Application/
COPY src/HelpDesk.Infrastructure/HelpDesk.Infrastructure.csproj src/HelpDesk.Infrastructure/
COPY src/HelpDesk.Api/HelpDesk.Api.csproj src/HelpDesk.Api/
COPY tests/HelpDesk.Tests/HelpDesk.Tests.csproj tests/HelpDesk.Tests/
RUN dotnet restore HelpDesk.sln

COPY . .
RUN dotnet build HelpDesk.sln -c Release --no-restore

# ---- Publish stage ----
FROM build AS publish
RUN dotnet publish src/HelpDesk.Api/HelpDesk.Api.csproj -c Release --no-restore -o /out

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=publish /out ./
ENV ASPNETCORE_URLS=http://+:8080
# Attachments volume (swap with Azure Blob storage in production).
VOLUME ["/app/uploads"]
EXPOSE 8080
ENTRYPOINT ["dotnet", "HelpDesk.Api.dll"]