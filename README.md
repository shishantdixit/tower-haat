# TowerHaat

Society ka apna bazaar — a platform for home businesses inside apartment societies: cloud kitchens, sellers, tuition and services. Starting with Ace Divino, Noida.

## Stack

ASP.NET Core (.NET 10) · EF Core · SQL Server · Angular PWA · SignalR · Web Push

See `CLAUDE.md` for architecture, rules and milestones.

## Prerequisites

- .NET SDK 10.0.100 or later (`global.json` pins the major version)
- Node.js 24 LTS (or 22.22.3+)
- SQL Server: LocalDB on Windows (installed with Visual Studio), or Docker anywhere else

## Production settings

Set these per environment (MonsterASP app settings or environment variables), never in committed files:

- `ConnectionStrings__Default`
- `Jwt__SigningKey`: random, at least 32 characters
- `Auth__Otp__HashKey`: random, at least 32 characters
- `Auth__AdminPhones__0`: your number

## Local setup

```bash
dotnet tool restore                 # installs dotnet-ef
cd web && npm ci && cd ..
```

### Database

**Windows with LocalDB:** nothing to configure. `appsettings.Development.json` points at `(localdb)\MSSQLLocalDB`.

**Docker (macOS, Linux, or Windows without LocalDB):**

```bash
docker compose up -d
dotnet user-secrets set "ConnectionStrings:Default" \
  "Server=localhost,1433;Database=TowerHaat;User Id=sa;Password=Dev_Passw0rd!;TrustServerCertificate=True" \
  --project src/TowerHaat.Api
```

Then create or update the schema:

```bash
dotnet ef database update --project src/TowerHaat.Infrastructure --startup-project src/TowerHaat.Api
```

`dotnet ef` uses the `ConnectionStrings__Default` environment variable when set, otherwise LocalDB.

## Signing in locally

SMS is not connected yet. In Development the login screen shows the code after "Get code" (it is also in the API log).

To make yourself a platform admin (society, tower and flat lists), add your number and sign in again:

```bash
dotnet user-secrets set "Auth:AdminPhones:0" "98765 43210" --project src/TowerHaat.Api
```

Then open `/admin`, choose Ace Divino and upload the flat list (`Tower,Flat` per line).

## Run

Two terminals:

```bash
dotnet run --project src/TowerHaat.Api        # API on http://localhost:5080
cd web && npm start                           # app on http://localhost:4200, /api proxied to 5080
```

Open http://localhost:4200. The home screen shows "Server: ok" when the API is reachable.

To try the production build served by the API itself (as on MonsterASP):

```bash
cd web && npm run build && cd ..              # writes into src/TowerHaat.Api/wwwroot
dotnet run --project src/TowerHaat.Api        # app and API on http://localhost:5080
```

The service worker is only active in production builds.

## Test

```bash
dotnet test                                   # xUnit: tenancy, auth, profile, admin and API tests (SQLite in memory)
cd web && npm test                            # Vitest
```

CI (`.github/workflows/ci.yml`) runs both on every push, checks formatting, builds the Angular app, and fails if the EF model has changes without a migration.

## Adding a migration

```bash
dotnet ef migrations add <FeatureName> --project src/TowerHaat.Infrastructure --startup-project src/TowerHaat.Api --output-dir Persistence/Migrations
```

## Layout

```
src/TowerHaat.Domain          entities and domain rules (no EF, no ASP.NET)
src/TowerHaat.Application     use cases (Auth, Residents, Societies), DTOs, interfaces
src/TowerHaat.Infrastructure  AppDbContext, tenancy filter, migrations, SMS sender
src/TowerHaat.Api             minimal API endpoints; serves the Angular build from wwwroot
tests/TowerHaat.Tests         xUnit
web/                          Angular PWA (customer, owner, admin lazy areas)
```
