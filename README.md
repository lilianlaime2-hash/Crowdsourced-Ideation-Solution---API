# CIS API

## Introduction
REST API that works as a forum. It manages topics, ideas, votes and statistics for user interaction.
Authentication is handled by the Users API. This API only validates the JSON Web Token that the Users API issues.

Built with C# .NET 10, Entity Framework Core 10 and MySQL.

## Requirements
- .NET SDK 10
- EF Core CLI: dotnet tool install --global dotnet-ef
- MySQL, we recommend Docker
- Users API running on http://localhost:8080

Create the MySQL container:
```bash
docker run -d --name sd3db -e MYSQL_ROOT_PASSWORD=sd5 -p 3307:3306 mysql
```

## First-time setup
Do this once after cloning. You do not need to repeat it every time you run the API.

**1. Save the secrets.** They are stored with User Secrets, not in appsettings.json, so they are never committed. Run from the solution folder:
```bash
cd CIS.API
dotnet user-secrets set "ConnectionStrings:LegacyDatabase" "server=localhost;port=3307;database=sd3;user=root;password=sd5"
dotnet user-secrets set "Jwt:SigningKey" "this-is-a-secret-key-of-at-least-32-characters"
cd ..
```
- The database must be **sd3**, the same schema the Users API uses. It holds the users table.
- Jwt:SigningKey must be exactly the same key the Users API uses to sign tokens. If they are different, every request returns 401.
- If a secret is missing, the API will not start. Check what is saved with dotnet user-secrets list --project CIS.API.

**2. Create the tables** in the database:
```bash
dotnet restore
dotnet ef database update --project CIS.DataAccess --startup-project CIS.API
```

## Usage
Every time you want to run the system, start these in order:

1. MySQL: docker start sd3db
2. Users API on http://localhost:8080
3. CIS API, from the solution folder:
```bash
dotnet run --project CIS.API
```
The API runs on http://localhost:5039.

Every endpoint requires a Bearer token in the Authorization header. You get the token from the Users API login.

## Project Structure
```
CIS.Domain       Entities and business rules
CIS.Application  Use cases, DTOs and interfaces
CIS.DataAccess   EF Core DbContext, migrations and repositories
CIS.API          Controllers, JWT authentication and Users API client
CIS.Tests        Unit tests with xUnit and Moq
```

## Log of Changes
- V1.0. October 2026: Initial version.

## Team
Imagine Devs: Lilian Laime, Josefina Carabajal, Alessandro Ruiz.
