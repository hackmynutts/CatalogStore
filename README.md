# CatalogStore

CatalogStore is a web-based management system for retail/catalog businesses (built for SAM DESIGN, a motorcycle-gear seller). The current build covers authentication, user administration, a status catalog, a full audit trail (event log), **clients**, **products with images and a customer-facing catalog**, an **import of the product catalog from the supplier's API**, a **public landing page**, and **inventory with per-warehouse stock tracking** (inventory lines, an append-only transaction ledger and an initial stock load from the supplier). Orders, sales and sellers are not started.

The solution is split into two ASP.NET Core apps:

- **CatalogStore.BackendAPI** — REST API, owns all data access and business logic.
- **CatalogStore.UI** — MVC front-end, talks to the API over HTTP; holds no direct database access of its own.

> Status: Authentication, Users, Status, Event Log, Clients, Products (+ images, catalog view, supplier import), Inventory, Inventory Lines and Inventory Transactions are implemented end-to-end (API + UI). Orders, Sales and Sellers are placeholder nav links only. Stock reservations and sales (`ProductoReservado`, `ProductoLiberado`, `Venta`) are supported by the transaction service but will only be triggered once Orders exist.

## Tech stack

**Backend (CatalogStore.BackendAPI)**
- **.NET 10** / ASP.NET Core Web API
- **Entity Framework Core 10** (SQL Server provider)
- **ASP.NET Core Identity** (`IdentityDbContext<ApplicationUser, ApplicationRole, Guid>`) for users/roles, with the default token providers (used by password reset)
- **JWT Bearer** authentication (`Microsoft.AspNetCore.Authentication.JwtBearer`)
- **Swagger / OpenAPI** (Swashbuckle) for API documentation, with Bearer auth wired into the UI
- **Humanizer** — turns enum values into Spanish display text (`TransactionType.ProductoReservado` → "Producto reservado"), honoring `[Display(Name)]`
- **`TimeProvider`** (`AppTimeProvider`) for all clock access, with the business time zone taken from configuration
- **Static files** — product images are stored on disk under `wwwroot/images/products/{productId}/` and served directly by the API
- **`IHttpClientFactory`** named clients for outbound calls: `"HaciendaApi"` (Costa Rica tax authority lookup) and `"ExternalCatalog"` (supplier product catalog and stock)

**Front-end (CatalogStore.UI)**
- **.NET 10** / ASP.NET Core MVC
- **Cookie authentication**, separate from the API's JWT — the UI decodes the JWT it receives at login, copies its claims into the auth cookie, and keeps the raw token as a custom claim
- **`IHttpClientFactory`** with a named `"BackendApi"` client and a custom `DelegatingHandler` that attaches the stored JWT as a `Bearer` header on every outgoing request
- **Bootstrap 5**, **SweetAlert2**, **jQuery Tablesorter** (client-side table search/sort/paging), **Font Awesome** — all via CDN except Bootstrap
- A bundled dataset of Costa Rica's provinces / cantons / districts (`wwwroot/data/cr-ubicaciones.json`) powering cascading address dropdowns

## Project structure

```
src/CatalogStore/
├── CatalogStore.BackendAPI/
│   ├── Controllers/    # API endpoints
│   ├── Data/            # ApplicationDBContext (EF Core + Identity)
│   ├── DTO/              # Request/response payloads, one folder per module
│   │   ├── CatalogExternal/   # Contract of the supplier's API (not our own)
│   │   └── Common/            # OperationResult (Success / Error / Id)
│   ├── Migrations/      # EF Core migrations
│   ├── Models/            # Domain entities (incl. ApplicationUser/ApplicationRole)
│   ├── Repository/       # Data access layer
│   ├── Services/          # Business logic (Client/Hacienda, Product/CatalogExternal, InventoryTransactions, DateManagement, ...)
│   └── wwwroot/images/    # Uploaded product images (created at runtime)
└── CatalogStore.UI/
    ├── Controllers/     # MVC controllers, call the API via HttpClientFactory
    ├── Handlers/         # JwtDelegatingHandler (attaches bearer token to API calls)
    ├── Models/            # View models, one folder per module
    ├── Views/              # Razor views
    └── wwwroot/            # Static assets (css/js/data/img); theme.css holds the design system, landing.css the public landing
```

### Conventions

- **One UI controller per API controller/entity** (e.g. `InventoryLineController` in both projects).
- List screens come in two flavors: `Index` (active records) and `IndexAdmin` (all records), both rendering the same view; the "all" variant and destructive actions are gated to `Admin`/`AdminIT`.
- Details pages share one layout (`.detail-header`, `.info-card`) and reserve "Próximamente" placeholder cards for features not built yet.
- Forms open in Bootstrap modals loaded as partials; tables that load by AJAX are initialized with `window.initAppTables(root)` (`wwwroot/js/table-init.js`).
- Every write goes through a Service that records an audit event and rethrows unexpected exceptions. Newer services return an `OperationResult` for business rule failures, which controllers map to `400 { message }`.
- **Dates:** everything is stored in UTC (`TimeProvider.GetUtcNow()`). A global `System.Text.Json` converter (`LocalDateTimeJsonConverter`) writes `DateTime` values to clients in Costa Rica local time and converts incoming local times back to UTC, so the UI never does time zone math.
- The UI mirrors backend enums by hand (the two projects don't share types): as plain `int` properties with hand-written `<select>` options, or as a copied enum with the same numeric values (`Models/InventoryTransaction/TransactionType.cs`).

## Implemented modules

**Public landing page** (`/`, anonymous)
- Customer-facing page for SAM DESIGN (products, shipping by courier, pickup in San José, contact links). Contact data comes from the UI's `Landing` configuration section.

**Authentication & Users**
- JWT login (`POST /api/User/login`) issuing a token with `Sub`, `UniqueName` (email), `Jti`, and one role claim per role.
- Admin-only user registration with an auto-generated temporary password (`MustChangePassword` flag forces a password change on first login).
- Self-service password change, admin-triggered password reset, full CRUD + role assignment.
- Roles: `AdminIT`, `Admin`, `Vendedor` (sales rep). All three are seeded automatically on startup in `Development`.

**Status** (generic catalog entity)
- Full CRUD. Read access for any authenticated role; write access restricted to `Admin`/`AdminIT`. `StatusID` 1 = active, 2 = inactive is used across every module.

**Event Log (audit trail)**
- Every meaningful write across the system — create/edit/inactivate/delete in every module, imports, login attempts, password changes — is recorded with actor, before/after JSON snapshots, and a stack trace on failures.
- Centralized in `EventlogServices.LogAsync(...)`: any Service calls it without knowing about HTTP context or JSON serialization — the "who" is resolved internally from the authenticated user's claims (with an override for anonymous flows like a failed login).
- UI is read-only: `/Eventlog` lists all events (`Admin`/`AdminIT` only) with a detail view for the before/after payload and stack trace.

**Clients** (`Admin`, `AdminIT`, `Vendedor`; deleting is `Admin`/`AdminIT` only)
- CRUD with inactivate (soft delete) and permanent delete.
- **Tax authority lookup:** typing a national ID queries Costa Rica's Hacienda API (`api.hacienda.go.cr`) to fill in the registered name and the taxpayer's economic-activity **CABYS** code.
- **Address selector:** cascading Province → Canton → District dropdowns (from the bundled dataset) that append to the free-text address.
- Credit terms and an optional delivery/courier partner per client.
- **Duplicate identification rule:** creating a client with an existing ID is blocked for non-admin roles, but allowed for `Admin`/`AdminIT`. There is deliberately no unique database constraint — the rule lives in the Service, and the role is passed in as a plain `bool` so Services stay free of claims logic.

**Products** (read: `Admin`, `AdminIT`, `Vendedor`; write: `Admin`, `AdminIT`)
- CRUD with inactivate and delete. Fields: code, name, description, category, unit price, price with IVA (13 %), unit of measure (`Pieza`, `Litro`, `Caja`, `Paquete`, `Unidad`). The details page shows both prices.
- Lookup by code: `GET /api/Product/code/{code}` and `/Product/DetailsByCode?code=...` (redirects to the regular details page).
- `ExternalProductID` (nullable, unique where not null) links a product to its record in the supplier's catalog; it is the key the import uses to decide insert vs. update.
- **Product images:** multi-image upload from the product details page (`.jpg`, `.jpeg`, `.png`, `.webp`), live gallery refresh without reloading, full-screen viewer, and an admin screen listing all images. Files live on disk; the database stores the web-relative URL.
- **Customer-facing catalog** (`/Product/Catalog`): card grid of active products with a category filter and per-card image carousel.

**Supplier catalog import** (`Admin`, `AdminIT`)
- `POST /api/Product/import?dryRun=true|false`, triggered from the products screen: a dry run shows a summary (new / updated / unchanged / skipped, with warnings and errors) and the admin confirms before anything is saved.
- `CatalogExternalServices` pages through the supplier API; `ExternalProductMapper` normalizes each row (trim, HTML-entity decoding, price rounding, zero price → `null`, unit and status mapping).
- Batched upsert per page with one `SaveChanges`, a lock against concurrent runs, and one audit entry with the summary. The import overwrites code, name, description and prices; it never touches category, unit or images, and it can deactivate a product but never reactivate it.
- Errors map to `409` (import already running), `502` (supplier error / invalid response) and `504` (supplier timeout).

**Inventory** (warehouses — `Admin`, `AdminIT` only)
- CRUD with inactivate and delete, active-only and all-records views. Reachable from the *Administración* menu.
- The details page lists the warehouse's **inventory lines** (loaded by AJAX, with search and paging) and the actions to add a product and to load stock from the supplier.

**Inventory Lines** (`InventoryLine_TB`, one row per warehouse × product)
- `Quantity` is the physical stock, `QuantityOnHold` the stock reserved by open orders, and **`QuantityAvailable` a stored computed column** (`Quantity − QuantityOnHold`), so it can never go out of sync. `QuantityRestock` is an optional low-stock threshold; the UI flags lines whose available stock reached it ("Reponer").
- Database `CHECK` constraints guarantee `Quantity ≥ 0`, `QuantityOnHold ≥ 0` and `QuantityOnHold ≤ Quantity`; a unique index enforces one line per (warehouse, product); `RowVersion` provides optimistic concurrency.
- Lines are created **with zero stock** — quantities never change through the line endpoints, only through transactions. Editing covers the restock threshold and status; a line can only be inactivated with no stock and no reservations.
- Line details page (`/InventoryLine/Details/{id}`): stock summary, movement history and audit data.

**Inventory Transactions** (`InventoryTransaction_TB`, append-only ledger)
- Every stock change is a transaction storing the type, quantity, the before/after snapshots of both `Quantity` and `QuantityOnHold`, a reference (`ReferenceReason` + `ReferenceID`), an optional reason, user and timestamp. Rows are never edited; corrections are new movements.
- `InventoryTransactionServices.RegisterAsync` is the **only code that moves stock**. It updates the line and inserts the transaction in a single `SaveChanges`, so both are saved or neither is. A concurrent change on the same line is detected through `RowVersion` and rejected with a retry message.
- Effects by type:

  | Type | Stock | Reserved | Rule |
  |---|---|---|---|
  | `CargaInicial` | +q | — | only on a line with no previous movements |
  | `IncrementoStock` | +q | — | also updates `LastRestock` |
  | `ProductoReservado` | — | +q | needs enough available stock |
  | `ProductoLiberado` | — | −q | cannot release more than reserved |
  | `Venta` | −q | −q | consumes a reservation |
  | `AjustePositivo` | +q | — | reason required |
  | `AjusteNegativo` | −q | — | reason required; cannot touch reserved stock |

- Manual movements (`POST /api/InventoryTransaction/register`) are limited to `IncrementoStock`, `AjustePositivo` and `AjusteNegativo`, and are always recorded as `AjusteManual`; reservations and sales are reserved for the future Orders module. The UI registers them from the line details page.
- History: `GET /api/InventoryTransaction/line/{inventoryLineId}`, newest first, with display names and signed changes already computed.

**Initial stock load from the supplier** (`Admin`, `AdminIT`)
- `POST /api/InventoryTransaction/import-stock/{inventoryId}?dryRun=true|false`, triggered from the warehouse details page with the same dry-run → confirm flow as the catalog import.
- Reads `inventario.total_disponible` for each supplier product. For each one it creates the line in the warehouse if missing and, when the supplier has stock, registers a `CargaInicial` (`ImportacionProveedor`, referencing the supplier's product ID).
- **It is an initial load, not a sync:** lines that already have movements are never touched, so it can be run again safely — a later run only picks up products that had no stock or were new.
- Per page it runs three queries and one `SaveChanges` (lines and transactions together). Products missing from the system (catalog not imported yet), inactive products or lines, and invalid quantities are skipped and reported.

## Not implemented yet

- Orders, Sales, Sellers/Vendors — present as nav links, no backing controllers/data. Orders will drive `ProductoReservado` / `ProductoLiberado` / `Venta`; at that point the transaction service will be split so an order and its reservation can be saved in the same `SaveChanges`.
- Ongoing stock sync with the supplier (only the initial load exists).
- "Prevent deleting the last Admin" safeguard.

## Known issues

- Products created or edited by hand before the `PriceCalcIVA` fix (`(dto.Price ?? 0) * IVA_RATE`) still store the price **without** IVA; re-saving them recalculates it. Imported products are not affected (the supplier provides the price with IVA).
- The `Unit` enum was renumbered (`Caja`=2, `Paquete`=3, `Unidad`=4) after data existed; the manual products `GUA-001` and `CAP-001` still hold the invalid values 5 and 6.
- `ProductCode` has no unique constraint, so a code lookup returns an arbitrary match if two products share a code (the supplier data has none today). About 40 % of supplier codes contain spaces.
- `showApiError` is duplicated across several UI scripts and should move to a shared file.

## Getting started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- SQL Server (Express or higher)

### Configuration

Both projects read secrets via [user-secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) in `Development` — nothing sensitive lives in `appsettings.json` (which is version-controlled).

**CatalogStore.BackendAPI** — `appsettings.json` holds the connection string, JWT issuer/audience, the business time zone and culture, and the non-secret external-catalog settings:

```json
{
  "ConnectionStrings": { "DefaultConnection": "Server=localhost\\SQLEXPRESS;Database=CatalogStoreDB;Trusted_Connection=True;TrustServerCertificate=True" },
  "Localization": { "TimeZoneId": "America/Costa_Rica", "Culture": "es-CR" },
  "ExternalCatalog": { "BaseUrl": "https://<supplier-host>/<path>/", "PageSize": 100 }
}
```

- `Localization:TimeZoneId` is an IANA id used by `AppTimeProvider` and the JSON date converter. `Localization:Culture` is applied with `UseRequestLocalization` (Spanish text from Humanizer). It is applied to the API only — in the UI it would change how decimals are parsed in forms.
- `ExternalCatalog:BaseUrl` **must end with `/`**. The `System.Net.Http.HttpClient` log category is set to `Warning` so request URLs (which carry the supplier token) are not written to the logs.

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
},
"Landing": {
  "WhatsApp": "<phone with country code>",
  "Instagram": "<@handle>",
  "Email": "<contact email>"
}
```

- `BaseUrl` is used for the UI server's calls to the API. Keep it on HTTPS: the API redirects HTTP to HTTPS, and a redirect breaks server-to-server calls.
- `PublicBaseUrl` is used to build **image URLs the browser loads directly**. It must be HTTPS because the UI is served over HTTPS and browsers block mixed content.
- `Landing` holds the contact links shown on the public landing page; empty values hide the corresponding link.

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

### First data load

1. Import the supplier catalog from the products screen (dry run first, then confirm).
2. Create a warehouse (e.g. `PRINCIPAL`) under *Administración → Inventario*.
3. Open the warehouse and use **Cargar stock del proveedor** (dry run first, then confirm).

## License

See [LICENSE](LICENSE).
