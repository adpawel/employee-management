# Build context is the repository root: the build needs Directory.Build.props,
# Directory.Packages.props and global.json alongside the project files.

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy only what restore needs first, so the package layer is cached until dependencies change.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/EmployeeManagement.Domain/EmployeeManagement.Domain.csproj src/EmployeeManagement.Domain/
COPY src/EmployeeManagement.Application/EmployeeManagement.Application.csproj src/EmployeeManagement.Application/
COPY src/EmployeeManagement.Infrastructure/EmployeeManagement.Infrastructure.csproj src/EmployeeManagement.Infrastructure/
COPY src/EmployeeManagement.Api/EmployeeManagement.Api.csproj src/EmployeeManagement.Api/
RUN dotnet restore src/EmployeeManagement.Api/EmployeeManagement.Api.csproj

COPY src/ src/
RUN dotnet publish src/EmployeeManagement.Api/EmployeeManagement.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app .
# The base image ships a non-root "app" user; running as root is unnecessary for a web API.
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "EmployeeManagement.Api.dll"]
