# CatalogStore

CatalogStore is a web-based management system for retail/catalog businesses. The current build covers authentication, user administration, a status catalog, and a full audit trail (event log). The sales domain (clients, orders, products, inventory, sellers/vendors) is scaffolded in navigation but not yet implemented.

The solution is split into two ASP.NET Core apps:

- **CatalogStore.BackendAPI** — REST API, owns all data access and business logic.
- **CatalogStore.UI** — MVC front-end, talks to the API over HTTP; holds no direct database access of its own.

> Status: authentication, user management, status catalog, and audit logging are implemented end-to-end (API + UI). Sales modules (Clients, Orders, Products, Inventory, Sellers) are placeholder nav links only — no controllers or data behind them yet.

## Tech stack

**Backend (CatalogStore.BackendAPI)**
- **.NET 10** / ASP.NET Core Web API
- **Entity Framework Core** (SQL Server provider)
- **ASP.NET Core Identity** (`IdentityDbContext<ApplicationUser, ApplicationRole, Guid>`) for users/roles
- **JWT Bearer** authentication (`Microsoft.AspNetCore.Authentication.JwtBearer`)
- **Swagger / OpenAPI** (Swashbuckle) for API documentation, with Bearer auth wired into the UI

**Front-end (CatalogStore.UI)**
- **.NET 10** / ASP.NET Core MVC
- **Cookie authentication**, separate from the API's JWT — the UI decodes the JWT it receives at login, copies its claims into the auth cookie, and keeps the raw token as a custom claim
- **`IHttpClientFactory`** with a named `"BackendApi"` client and a custom `DelegatingHandler` that attaches the stored JWT as a `Bearer` header on every outgoing request
- **Bootstrap 5**, **SweetAlert2**, **jQuery Tablesorter** (client-side table search/sort), **Font Awesome** — all via CDN except Bootstrap

## Project structure

```
src/CatalogStore/
├── CatalogStore.BackendAPI/
│   ├── Controllers/    # API endpoints
│   ├── Data/            # ApplicationDBContext (EF Core + Identity)
│   ├── DTO/              # Request/response payloads, one folder per module
│   ├── Migrations/      # EF Core migrations
│   ├── Models/            # Domain entities (incl. ApplicationUser/ApplicationRole)
│   ├── Repository/       # Data access layer
│   └── Services/          # Business logic
└── CatalogStore.UI/
    ├── Controllers/     # MVC controllers, call the API via HttpClientFactory
    ├── Handlers/         # JwtDelegatingHandler (attaches bearer token to API calls)
    ├── Models/            # View models, one folder per module
    ├── Views/              # Razor views
    └── wwwroot/            # Static assets (css/js), theme.css holds the design system
```

## Implemented modules

**Authentication & Users**
- JWT login (`POST /api/User/login`) issuing a token with `Sub`, `UniqueName` (email), `Jti`, and one role claim per role.
- Admin-only user registration with an auto-generated temporary password (`MustChangePassword` flag forces a password change on first login).
- Self-service password change, admin-triggered password reset, full CRUD + role assignment for `Admin`/`AdminIT`.
- Roles: `AdminIT`, `Admin`, `Vendedor`, `Cliente`. `AdminIT` and `Admin` are seeded automatically on startup in `Development`.

**Status** (generic catalog entity)
- Full CRUD. Read access for any authenticated role; write access restricted to `Admin`/`AdminIT`.

**Event Log (audit trail)**
- Every meaningful write across the system — user and status create/edit/delete, login attempts, password changes — is recorded with actor, before/after JSON snapshots, and a stack trace on failures.
- Centralized in `EventlogServices.LogAsync(...)`: any Service calls it without knowing about HTTP context or JSON serialization — the "who" is resolved internally from the authenticated user's claims (with an override for anonymous flows like a failed login).
- UI is read-only: `/Eventlog` lists all events (`Admin`/`AdminIT` only) with a detail view for the before/after payload and stack trace. Events are never created from the UI — only from server-side business logic in the API.

**Not implemented yet**
- Clients, Orders, Products, Inventory, Sellers — present as nav links under "Ventas", no backing controllers/data.
- "Prevent deleting the last Admin" safeguard.

## Getting started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- SQL Server (Express or higher)

### Configuration

Both projects read secrets via [user-secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) in `Development` — nothing sensitive lives in `appsettings.json`.

**CatalogStore.BackendAPI** — `appsettings.json` holds the connection string and JWT issuer/audience; set the signing key and seed admin via user-secrets:

```json
{
  "Jwt": { "Key": "<a long random string>" },
  "SeedAdmin": {
    "UserName": "admin",
    "Email": "admin@example.com",
    "FullName": "Administrator",
    "Password": "<a password meeting Identity's default policy>"
  }
}
```

**CatalogStore.UI** — `appsettings.json`'s `BackendApi:BaseUrl` must point at the running API instance.

### Run locally

Both projects need to run at the same time — the UI has no functionality without the API behind it.

```bash
cd src/CatalogStore/CatalogStore.BackendAPI
dotnet restore
dotnet run
```

```bash
cd src/CatalogStore/CatalogStore.UI
dotnet restore
dotnet run
```

Or set both as startup projects in Visual Studio (`Configure Startup Projects... → Multiple startup projects`).

- **API**: `https://localhost:7133` — Swagger UI available at the root in `Development`.
- **UI**: `https://localhost:7298`.

On first run in `Development`, the API applies pending EF Core migrations automatically and seeds the `AdminIT`/`Admin` roles plus the admin account configured under `SeedAdmin:*`.

## License

See [LICENSE](LICENSE).
