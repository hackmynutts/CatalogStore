# CatalogStore

CatalogStore is a web-based management system for retail/catalog businesses (built for a motorcycle-parts seller). The current build covers authentication, user administration, a status catalog, a full audit trail (event log), **clients**, **products with images and a customer-facing catalog**, and the **inventory (warehouse) catalog**. Stock tracking (inventory lines and transactions) and an import of the product catalog from an external supplier API are in progress; orders, sales and sellers are not started.

The solution is split into two ASP.NET Core apps:

- **CatalogStore.BackendAPI** — REST API, owns all data access and business logic.
- **CatalogStore.UI** — MVC front-end, talks to the API over HTTP; holds no direct database access of its own.

> Status: Authentication, Users, Status, Event Log, Clients, Products (+ images, catalog view) and Inventory (warehouse catalog) are implemented end-to-end (API + UI). `InventoryLine` (per-warehouse stock) has its model, database table and constraints in place but no service/controller/UI yet. The external catalog import is partially built (see [External catalog import](#external-catalog-import-in-progress)). Orders, Sales and Sellers are placeholder nav links only.

## Tech stack

**Backend (CatalogStore.BackendAPI)**
- **.NET 10** / ASP.NET Core Web API
- **Entity Framework Core 10** (SQL Server provider)
- **ASP.NET Core Identity** (`IdentityDbContext<ApplicationUser, ApplicationRole, Guid>`) for users/roles, with the default token providers (used by password reset)
- **JWT Bearer** authentication (`Microsoft.AspNetCore.Authentication.JwtBearer`)
- **Swagger / OpenAPI** (Swashbuckle) for API documentation, with Bearer auth wired into the UI
- **Static files** — product images are stored on disk under `wwwroot/images/products/{productId}/` and served directly by the API
- **`IHttpClientFactory`** named clients for outbound calls: `"HaciendaApi"` (Costa Rica tax authority lookup) and `"ExternalCatalog"` (supplier product catalog)

**Front-end (CatalogStore.UI)**
- **.NET 10** / ASP.NET Core MVC
- **Cookie authentication**, separate from the API's JWT — the UI decodes the JWT it receives at login, copies its claims into the auth cookie, and keeps the raw token as a custom claim
- **`IHttpClientFactory`** with a named `"BackendApi"` client and a custom `DelegatingHandler` that attaches the stored JWT as a `Bearer` header on every outgoing request
- **Bootstrap 5**, **SweetAlert2**, **jQuery Tablesorter** (client-side table search/sort), **Font Awesome** — all via CDN except Bootstrap
- A bundled dataset of Costa Rica's provinces / cantons / districts (`wwwroot/data/cr-ubicaciones.json`) powering cascading address dropdowns

## Project structure

```
src/CatalogStore/
├── CatalogStore.BackendAPI/
│   ├── Controllers/    # API endpoints
│   ├── Data/            # ApplicationDBContext (EF Core + Identity)
│   ├── DTO/              # Request/response payloads, one folder per module
│   │   └── CatalogExternal/   # Contract of the supplier's API (not our own)
│   ├── Migrations/      # EF Core migrations
│   ├── Models/            # Domain entities (incl. ApplicationUser/ApplicationRole)
│   ├── Repository/       # Data access layer
│   ├── Services/          # Business logic (Client/Hacienda, Product/CatalogExternal, ...)
│   └── wwwroot/images/    # Uploaded product images (created at runtime)
└── CatalogStore.UI/
    ├── Controllers/     # MVC controllers, call the API via HttpClientFactory
    ├── Handlers/         # JwtDelegatingHandler (attaches bearer token to API calls)
    ├── Models/            # View models, one folder per module
    ├── Views/              # Razor views
    └── wwwroot/            # Static assets (css/js/data); theme.css holds the design system
```

### Conventions

- **One UI controller per API controller/entity** (e.g. `InventoryController` in both projects).
- List screens come in two flavors: `Index` (active records) and `IndexAdmin` (all records), both rendering the same view; the "all" variant and destructive actions are gated to `Admin`/`AdminIT`.
- Details pages share one layout (`.detail-header`, `.info-card`) and reserve "Próximamente" placeholder cards for features not built yet.
- Every write goes through a Service that records an audit event (success and failure paths) and rethrows unexpected exceptions.
- Timestamps are stored with `DateTime.UtcNow`.
- The UI mirrors backend enums as plain `int` view-model properties with hand-written `<select>` options (the two projects don't share types).

## Implemented modules

**Authentication & Users**
- JWT login (`POST /api/User/login`) issuing a token with `Sub`, `UniqueName` (email), `Jti`, and one role claim per role.
- Admin-only user registration with an auto-generated temporary password (`MustChangePassword` flag forces a password change on first login).
- Self-service password change, admin-triggered password reset, full CRUD + role assignment.
- Roles: `AdminIT`, `Admin`, `Vendedor` (sales rep). All three are seeded automatically on startup in `Development`.

**Status** (generic catalog entity)
- Full CRUD. Read access for any authenticated role; write access restricted to `Admin`/`AdminIT`. `StatusID` 1 = active, 2 = inactive is used across every module.

**Event Log (audit trail)**
- Every meaningful write across the system — create/edit/inactivate/delete in every module, login attempts, password changes — is recorded with actor, before/after JSON snapshots, and a stack trace on failures.
- Centralized in `EventlogServices.LogAsync(...)`: any Service calls it without knowing about HTTP context or JSON serialization — the "who" is resolved internally from the authenticated user's claims (with an override for anonymous flows like a failed login).
- UI is read-only: `/Eventlog` lists all events (`Admin`/`AdminIT` only) with a detail view for the before/after payload and stack trace.

**Clients** (`Admin`, `AdminIT`, `Vendedor`; deleting is `Admin`/`AdminIT` only)
- CRUD with inactivate (soft delete) and permanent delete.
- **Tax authority lookup:** typing a national ID queries Costa Rica's Hacienda API (`api.hacienda.go.cr`) to fill in the registered name and the taxpayer's economic-activity **CABYS** code.
- **Address selector:** cascading Province → Canton → District dropdowns (from the bundled dataset) that append to the free-text address.
- Credit terms and an optional delivery/courier partner per client.
- **Duplicate identification rule:** creating a client with an existing ID is blocked for non-admin roles, but allowed for `Admin`/`AdminIT`. There is deliberately no unique database constraint — the rule lives in the Service, and the role is passed in as a plain `bool` so Services stay free of claims logic.

**Products** (read: `Admin`, `AdminIT`, `Vendedor`; write: `Admin`, `AdminIT`)
- CRUD with inactivate and delete. Fields: code, name, description, category, unit price, price with IVA, unit of measure (`Pieza`, `Litro`, `Caja`, `Paquete`, `Unidad`).
- `ExternalProductID` (nullable, unique where not null) links a product to its record in the supplier's catalog; it is the key the import uses to decide insert vs. update.
- **Product images:** multi-image upload from the product details page (`.jpg`, `.jpeg`, `.png`, `.webp`), live gallery refresh without reloading, full-screen viewer, and an admin screen listing all images. Files live on disk; the database stores the web-relative URL.
- **Customer-facing catalog** (`/Product/Catalog`): card grid of active products with a category filter and per-card image carousel.

**Inventory** (warehouse/location catalog — `Admin`, `AdminIT` only)
- CRUD with inactivate and delete, active-only and all-records views.
- Details page with reserved space for stock lines and transactions (placeholders until those features exist).

## In progress

### Stock tracking: `InventoryLine` and `InventoryTransactions`

Design decisions already made and reflected in the schema:

- One `InventoryLine` per (warehouse, product) pair — enforced by a unique index.
- `Quantity` is the physical stock and never changes on a reservation. `QuantityOnHold` is stock reserved by open orders. **`QuantityAvailable` is a stored computed column** (`Quantity − QuantityOnHold`), so it can never go out of sync.
- Reservation flow: order opened → `QuantityOnHold` up; order completed → both `Quantity` and `QuantityOnHold` down; order cancelled or expired → `QuantityOnHold` down.
- `RowVersion` (optimistic concurrency) prevents two simultaneous reservations from overselling the last unit.
- Database `CHECK` constraints guarantee `Quantity ≥ 0`, `QuantityOnHold ≥ 0` and `QuantityOnHold ≤ Quantity`.
- `InventoryTransactions` will be an append-only ledger (entry, exit, adjustment, transfer, reserve, release) referencing the order/sale that caused each movement; corrections are made with a reversing transaction, never by editing.

Not built yet: repository, service, controller and UI for both entities.

### External catalog import (in progress)

Goal: load the supplier's ~480-product catalog into `Products_TB`, then load stock into `InventoryLine` once that feature exists.

Done:
- Configuration (`ExternalCatalog:*`), `"ExternalCatalog"` named HTTP client, and `CatalogExternalServices.GetProductsPageAsync(page, pageSize)` — a thin adapter that pages through the supplier API and throws on HTTP errors (with the status code) or on `ok: false` responses.
- Contract DTOs (`DTO/CatalogExternal`) and the `ProductImportItem` intermediate model that carries the normalized product plus warnings/errors.
- `Product.ExternalProductID` with its unique filtered index.

Pending: the mapper (external → normalized product: trim, HTML-entity decoding, price rounding, zero price → `null`, unit and status mapping), the import service (batched upsert per page, single audit entry with a summary), and an `Admin`/`AdminIT` `POST` endpoint with a dry-run option.

## Not implemented yet

- Orders, Sales, Sellers/Vendors — present as nav links, no backing controllers/data.
- Services, endpoints and UI for `InventoryLine` / `InventoryTransactions`.
- "Prevent deleting the last Admin" safeguard.

## Known issues

- `ProductServices.AddAsync` and `UpdateAsync` compute `PriceCalcIVA` with `dto.Price ?? 0 * IVA_RATE`; operator precedence makes this evaluate to `dto.Price ?? 0`, so manually created/edited products store the price **without** IVA. Should be `(dto.Price ?? 0) * IVA_RATE`.
- The `Unit` enum was renumbered (`Caja`=2, `Paquete`=3, `Unidad`=4) after data existed; rows created before the change need their `UnidadMedida` values remapped.
- `InventoryServices` and `ProductImageServices` still stamp records with `DateTime.Now` instead of `DateTime.UtcNow`, unlike the rest of the codebase.

## Getting started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- SQL Server (Express or higher)

### Configuration

Both projects read secrets via [user-secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) in `Development` — nothing sensitive lives in `appsettings.json` (which is version-controlled).

**CatalogStore.BackendAPI** — `appsettings.json` holds the connection string, JWT issuer/audience and the non-secret external-catalog settings:

```json
{
  "ConnectionStrings": { "DefaultConnection": "Server=localhost\\SQLEXPRESS;Database=CatalogStoreDB;Trusted_Connection=True;TrustServerCertificate=True" },
  "ExternalCatalog": { "BaseUrl": "https://<supplier-host>/<path>/", "PageSize": 100 }
}
```

`ExternalCatalog:BaseUrl` **must end with `/`**. The `System.Net.Http.HttpClient` log category is set to `Warning` so request URLs (which carry the supplier token) are not written to the logs.

Secrets, set from the `CatalogStore.BackendAPI` folder:

```bash
dotnet user-secrets set "Jwt:Key" "<a long random string>"
dotnet user-secrets set "SeedAdmin:UserName" "admin"
dotnet user-secrets set "SeedAdmin:Email" "admin@example.com"
dotnet user-secrets set "SeedAdmin:FullName" "Administrator"
dotnet user-secrets set "SeedAdmin:Password" "<a password meeting Identity's default policy>"
dotnet user-secrets set "ExternalCatalog:Token" "<supplier API token>"
```

In production the same keys are supplied as environment variables using a double underscore (e.g. `ExternalCatalog__Token`), since user-secrets are only loaded in `Development`.

**CatalogStore.UI** — `appsettings.json`:

```json
"BackendApi": {
  "BaseUrl": "https://localhost:7133/",
  "PublicBaseUrl": "https://localhost:7133/"
}
```

- `BaseUrl` is used for the UI server's calls to the API. Keep it on HTTPS: the API redirects HTTP to HTTPS, and a redirect breaks server-to-server calls.
- `PublicBaseUrl` is used to build **image URLs the browser loads directly**. It must be HTTPS because the UI is served over HTTPS and browsers block mixed content.

### Run locally

Both projects need to run at the same time — the UI has no functionality without the API behind it. Use the **`https` launch profile** for both; the default first profile only listens on HTTP.

```bash
cd src/CatalogStore/CatalogStore.BackendAPI
dotnet restore
dotnet run --launch-profile https
```

```bash
cd src/CatalogStore/CatalogStore.UI
dotnet restore
dotnet run --launch-profile https
```

Or set both as startup projects in Visual Studio (`Configure Startup Projects... → Multiple startup projects`) using the `https` profile.

- **API**: `https://localhost:7133` (HTTP `5081`) — Swagger UI at `/swagger` in `Development`.
- **UI**: `https://localhost:7298` (HTTP `5238`).

On startup in `Development`, the API applies pending EF Core migrations automatically and seeds the `AdminIT`, `Admin` and `Vendedor` roles plus the admin account configured under `SeedAdmin:*`. Migrations and seeding do **not** run outside `Development`.

## License

See [LICENSE](LICENSE).
