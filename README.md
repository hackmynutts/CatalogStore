# CatalogStore

CatalogStore is a web-based management system for retail/catalog businesses, built for **SAM DESIGN**, a motorcycle-gear seller in Costa Rica. It covers the whole sales cycle: clients, products with images and a public catalog, supplier import, inventory with an append-only stock ledger, orders, sales with payments, defective-product exchanges, sales documents (proforma, invoice, receipt), seller goals, Excel reports, password/email flows and an audit trail.

The solution is split into two ASP.NET Core apps:

- **CatalogStore.BackendAPI** — REST API, owns all data access and business logic.
- **CatalogStore.UI** — MVC front-end, talks to the API over HTTP; holds no direct database access of its own.

> Status: everything below is implemented end-to-end (API + UI) and deployed to Azure at https://samdesigncr.com (API at https://api.samdesigncr.com). Electronic invoicing is built but **disabled** in production (`Invoicing:Enabled=false`) and has only been tested against a fake provider.

## Tech stack

**Backend (CatalogStore.BackendAPI)**
- **.NET 10** / ASP.NET Core Web API
- **Entity Framework Core 10** (SQL Server provider)
- **ASP.NET Core Identity** (`IdentityDbContext<ApplicationUser, ApplicationRole, Guid>`) with default token providers plus an own `InvitationTokenProvider` (48 h invitation links)
- **JWT Bearer** authentication, with refresh
- **Swagger / OpenAPI** (Swashbuckle) with Bearer auth wired in
- **Humanizer** — turns enum values into Spanish display text, honoring `[Display(Name)]`
- **MailKit** — SMTP email, sent through a database outbox (`EmailOutbox_TB`) processed by a background worker with retries
- **DocumentFormat.OpenXml** — Excel reports
- **`TimeProvider`** (`AppTimeProvider`) for all clock access, with the business time zone taken from configuration
- **Hosted workers** — email outbox, and hourly expiration of stale Pending orders
- **Static files** — product images stored on disk under `wwwroot/images/products/{productId}/`
- **`IHttpClientFactory`** named clients: `"HaciendaApi"` (tax authority lookups and CABYS search), `"ExternalCatalog"` (supplier catalog and stock), and the invoicing provider transport

**Front-end (CatalogStore.UI)**
- **.NET 10** / ASP.NET Core MVC
- **Cookie authentication**, separate from the API's JWT — the UI decodes the JWT at login, copies its claims into the cookie and keeps the raw token as a custom claim
- **`IHttpClientFactory`** with a `"BackendApi"` client and a `DelegatingHandler` that attaches the stored JWT
- **QuestPDF** — PDF downloads (proforma, invoice, sales receipt, public consultation list)
- **Bootstrap 5**, **SweetAlert2**, **jQuery Tablesorter** (search/sort/paging), **Font Awesome**
- Bundled Costa Rica provinces / cantons / districts dataset (`wwwroot/data/cr-ubicaciones.json`)
- Rate limiting on public endpoints (forgot password, consultation PDF)

## Project structure

```
src/CatalogStore/
├── CatalogStore.BackendAPI/
│   ├── Controllers/    # API endpoints (User, Client, Product, ProductImage, PublicCatalog, Inventory*, Order, Sale, Invoice, Goal, Report, Eventlog, Status, Hacienda)
│   ├── Data/            # ApplicationDBContext (EF Core + Identity)
│   ├── DTO/              # Request/response payloads, one folder per module
│   ├── Migrations/      # EF Core migrations
│   ├── Models/            # Domain entities
│   ├── Repository/       # Data access layer
│   ├── Services/          # Business logic (Orders, Sales, Invoicing, Goals, Reports, Email, Client/CreditBlock, ...)
│   └── wwwroot/images/    # Uploaded product images (created at runtime)
└── CatalogStore.UI/
    ├── Controllers/     # MVC controllers, call the API via HttpClientFactory (plus SeoController)
    ├── Documents/        # QuestPDF documents
    ├── Handlers/         # JwtDelegatingHandler, ApiErrorReader
    ├── Models/            # View models, one folder per module
    ├── Views/              # Razor views
    └── wwwroot/            # Static assets; theme.css is the design system, landing.css the public landing
```

### Conventions

- **One UI controller per API controller/entity.**
- List screens come in two flavors: `Index` (active records) and `IndexAdmin` (all records); the "all" variant and destructive actions are gated to `Admin`/`AdminIT`.
- Forms open in Bootstrap modals loaded as partials; AJAX tables are initialized with `window.initAppTables(root)` (`wwwroot/js/table-init.js`). Paged tables scroll to the table start on page change and keep their page after a reload.
- Every write goes through a Service that records an audit event. Services return an `OperationResult` for business-rule failures, which controllers map to `400 { message }`. The UI shows API errors in SweetAlert (`window.showApiError`, `ApiErrorReader`).
- **Dates:** stored in UTC. A global JSON converter writes `DateTime` to clients in Costa Rica local time and converts incoming local times back to UTC.
- **Roles are passed to Services as plain `bool`s** (e.g. `isAdmin`), so Services stay free of claims logic. Rules (discount cap, ownership) are enforced server-side, never trusted from the client.
- The UI mirrors backend enums by hand (the projects don't share types). Product categories live in one UI list, `Models/Product/ProductCategories.cs`.
- Roles: `AdminIT`, `Admin`, `Vendedor` (seller). A seller only sees and works on their own orders, sales and goals.

## Implemented modules

**Public site** (anonymous)
- Landing page (`/`) with contact links from the `Landing` configuration, and a public catalog page (`/Home/Catalog`): the visitor builds a consultation list (no quantities, no availability), downloads it as PDF or sends it by WhatsApp. Data comes from the anonymous, cached `GET api/PublicCatalog`.
- **SEO and share preview:** title/description per page, Open Graph and Twitter tags, favicons and web manifest, JSON-LD (WebSite + Organization), `/robots.txt` and `/sitemap.xml`, and a 301 from `www` to the main host. Only the landing and the catalog are indexable, and only when `Seo:SiteUrl` is configured; with it empty, every page is `noindex` and `robots.txt` disallows everything, so test copies never get indexed.

**Authentication & Users**
- JWT login. Roles are seeded in `Development`.
- **No temporary passwords:** admin registration creates the user without a password and emails an invitation link; password reset uses 60-minute links; links are single use. Forgot password is public (same answer always, throttled per user and per IP). With `Email:Enabled=false` the API returns the link to the admin instead of emailing.
- Per-user `SendNotifications` flag for optional emails. Pending invitations are flagged in the users list, with resend.

**Event Log (audit trail)** — every meaningful write is recorded with actor, before/after JSON and stack trace on failures, through `EventlogServices.LogAsync`. UI is read-only (`Admin`/`AdminIT`).

**Status** — generic status catalog (`StatusID` 1 = active, 2 = inactive across all modules).

**Clients**
- CRUD with inactivate and delete; Hacienda lookup by national ID (registered name and economic activity); cascading province/canton/district selector; credit terms and courier partner.
- Duplicate identification is blocked for non-admin roles (rule in the Service, no unique constraint).
- **Credit block:** a client with 2 or more overdue sales (`Orders:BlockOverdueSalesFrom`, 0 = off) cannot start an order, add to a new order or complete one. Computed live; payments stay allowed. `Admin`/`AdminIT` can grant an override with a reason and an end date (max 90 days). Existing in-process orders can still be opened.
- Client statement (account statement) as Excel.

**Products**
- CRUD, price with IVA (13 %), unit of measure, category, optional `CostPrice` (admin only, hidden from sellers), CABYS code. `Price` is the sale price without IVA and already includes the profit.
- Image upload (`.jpg`, `.jpeg`, `.png`, `.webp`) with gallery and viewer; customer-facing catalog with category filter, availability ("N disponibles", low, "Agotado") and a quantity stepper on each card.
- **CABYS tooling** (admin): Hacienda CABYS search, per-product assignment and bulk assignment by category.
- **Supplier catalog import** (`Admin`, `AdminIT`): dry run, then confirm. Overwrites code, name, description and prices; never touches category, unit, cost or images; can deactivate but never reactivate.

**Inventory, Inventory Lines, Inventory Transactions**
- A single warehouse. Lines hold `Quantity`, `QuantityOnHold` and a stored computed `QuantityAvailable`, with `CHECK` constraints and `RowVersion` concurrency. Quantities change **only** through transactions.
- `InventoryTransactionServices.RegisterAsync` is the only code that moves stock. Types: `CargaInicial`, `IncrementoStock`, `ProductoReservado`, `ProductoLiberado`, `Venta`, `AjustePositivo`, `AjusteNegativo`, `Devolucion` (stock comes back when a sale is voided) and `Merma` (defective unit discarded in an exchange).
- Initial stock load from the supplier (dry run → confirm); it never touches lines that already have movements.

**Orders**
- States: InProcess → Pending → Completed, plus Cancelled and Returned (Rejected/Shipped are reserved). Numbers `SAM0000001` from a SQL sequence.
- Entry points: the catalog "+" (adds to the user's active order) or "Iniciar orden" on the client profile. A client has at most one InProcess order. Stock is reserved while the order is open and consumed with `Venta` when it completes; cancel releases it.
- Prices are recalculated on Pending and again on Complete; if something changed, the order does not complete and the differences are returned.
- Discount per line, or one percentage for the whole order (InProcess only). Sellers are capped at `Orders:MaxSellerDiscountPercent`.
- With/without IVA is decided per order (InProcess only).
- Pending orders older than `Orders:PendingExpirationDays` (8) are cancelled automatically by `OrderExpirationWorker`; the seller gets a warning at `PendingWarningDays` (6).
- **Proforma:** non-fiscal quote (`PRO-…`, valid 8 days) as a printable page and PDF download, with email.

**Sales**
- Created in the same `SaveChanges` as order completion (`VTA0000001`). Credit terms set the due date; contado sales start Pending until paid.
- **Payments (abonos):** methods follow Hacienda codes; never edited, only voided by an admin with a reason. Paid amount and status are recomputed from non-voided payments.
- **Void ("Anular venta")**: admin only, reason required, blocked while the sale has active payments or an accepted invoice without an accepted credit note. The order goes to Returned and stock returns (`Devolucion`).
- **Exchange of defective product** (`SalesExchange_TB`): admin picks a line, a quantity and a reason; the defective unit leaves stock as `Merma`. Same product only, no change to totals or invoice.
- Sale list with KPI tiles and filters (pending, overdue, voided, ...); a client's sales and pending balance on the client page.
- **Documents:** with IVA → electronic invoice (factura); without IVA → *Comprobante de venta*, a non-fiscal document in the house format with print and PDF download. Accepted invoices have a print page and PDF in the same format.

**Electronic invoicing** (built, disabled by default)
- Manual "Facturar" button from the sale; `Invoice_TB` stores every attempt **before** the POST to the provider; one emission at a time; timeouts are marked *Uncertain* and never retried automatically; credit notes (`03`) for voids; XML and email. Configuration under `Invoicing`. Only tested against a fake provider.

**Seller goals** — monthly goals per seller (amount sold, amount collected, number of sales), keyed by seller email; "Mis metas" for sellers and "Metas del equipo" with copy-previous-month for admins.

**Reports** (Excel, `Operación → Ventas → Reportes`) — sales, receivables (aging), payments, products (historic cost), goals, valued stock and client statement. Sellers get only their own sales, receivables and payments; cost-based reports are admin-only.

**Email** — outbox with retries (1/5/15/60 min). Templates: invitation, password reset, password changed, proforma, payment voided, order expired/expiry warning, invoice.

## Known issues and gaps

- Product categories were renumbered after data existed: older rows may need a one-time `UPDATE` to the new numbers (0 Genérica, 1 Puños para moto, 2 Guantes de moto, 3 Capas de moto, 4 Plásticos de moto, 5 Caucho campana, 6 Jacket).
- JWT/refresh do not check the SecurityStamp, so a password reset does not close sessions that are already open.
- Sales and goals are keyed by the seller's email: editing a user's email orphans their history.
- Profit reports do not include the cost of units discarded as `Merma`.
- The system applies 13 % IVA to everything; per-product rates and exonerated clients are not modeled.
- Invoicing is single-instance only (in-process lock) and untested against the real provider.
- "Prevent deleting the last Admin" safeguard is missing.

## Getting started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- SQL Server (Express or higher)

### Configuration

Both projects read secrets via [user-secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) in `Development`; nothing sensitive lives in `appsettings.json`. In production the same keys are supplied as environment variables / App Service settings with a double underscore (e.g. `ExternalCatalog__Token`).

**CatalogStore.BackendAPI** — `appsettings.json` holds the connection string, JWT issuer/audience, `Localization` (time zone and culture), `ExternalCatalog` (`BaseUrl` must end with `/`), and the non-secret options of `Orders`, `Email` and `Invoicing`.

Main option sections:

| Section | Purpose |
|---|---|
| `Orders` | `MaxSellerDiscountPercent`, `PendingExpirationDays`, `PendingWarningDays`, `BlockOverdueSalesFrom` |
| `Email` | `Enabled`, SMTP host/port/user, `UiBaseUrl` (public UI address used in links); the password is a secret (`Email__Password`) |
| `Invoicing` | `Enabled` (false by default), endpoint, profile, issuer, activity, branch/terminal |

Secrets, set from the `CatalogStore.BackendAPI` folder:

```bash
dotnet user-secrets set "Jwt:Key" "<a long random string>"
dotnet user-secrets set "SeedAdmin:UserName" "admin"
dotnet user-secrets set "SeedAdmin:Email" "admin@example.com"
dotnet user-secrets set "SeedAdmin:FullName" "Administrator"
dotnet user-secrets set "SeedAdmin:Password" "<a password meeting the password policy (min. 8 characters)>"
dotnet user-secrets set "ExternalCatalog:Token" "<supplier API token>"
dotnet user-secrets set "Email:Password" "<SMTP app password>"
```

**CatalogStore.UI** — `appsettings.json`:

```json
"BackendApi": {
  "BaseUrl": "https://localhost:7133/",
  "PublicBaseUrl": "https://localhost:7133/"
},
"Landing": { "WhatsApp": "<phone with country code>", "Instagram": "<@handle>", "Email": "<contact email>" },
"Seo": { "SiteUrl": "" }
```

- `BaseUrl` is for the UI server's calls to the API (keep it HTTPS: a redirect breaks server-to-server calls). `PublicBaseUrl` builds image URLs the browser loads directly (must be HTTPS).
- `Landing` empty values hide the corresponding link.
- `Seo:SiteUrl` is the public address (e.g. `https://samdesigncr.com`, no trailing slash). Leave it empty everywhere except production.

### Run locally

Both projects must run at the same time. Use the **`https` launch profile** for both.

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

- **API**: `https://localhost:7133` — Swagger at `/swagger` in `Development`.
- **UI**: `https://localhost:7298`.

In `Development` the API applies pending EF Core migrations and seeds the roles and the `SeedAdmin:*` account on startup. **Neither runs outside `Development`.**

### Deploying (Azure App Service, Linux)

- Two App Services (UI and API). Set `ConnectionStrings__DefaultConnection` (type SQLAzure), `Jwt__Key`, `Email__*`, `ExternalCatalog__*`; enable Always On and HTTPS Only.
- UI: `BackendApi__BaseUrl`, `BackendApi__PublicBaseUrl`, `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`, `Seo__SiteUrl`.
- API: `Email__UiBaseUrl` with the public UI address, `Invoicing__Enabled=false`.
- **First deploy (empty database):** because migrations and seeding only run in `Development`, set `ASPNETCORE_ENVIRONMENT=Development` on the API once, start it so it migrates and creates the admin, then set it back to `Production` and delete the `SeedAdmin__*` settings. Later schema changes need the same step or `dotnet ef database update`.
- After a deploy that includes new migrations, apply them before using the new features.

### First data load

1. Import the supplier catalog from the products screen (dry run first, then confirm).
2. Create the warehouse (e.g. `PRINCIPAL`) under *Administración → Inventario*.
3. Open the warehouse and use **Cargar stock del proveedor** (dry run first, then confirm).
4. Optional: assign CABYS codes (*Product/Cabys*) and set product cost prices.

## License

See [LICENSE](LICENSE).
