# CatalogStore

CatalogStore is a web-based management system for retail/catalog businesses, covering inventory, the full sales flow (client selection → order → proforma → invoicing), and seller/vendor tracking.

Built with **C# / ASP.NET Core** and **SQL Server**, exposed as a REST API.

> ⚠️ Status: early scaffolding. Core domain models, persistence, and auth are not implemented yet.

## Tech stack

- **.NET 10** / ASP.NET Core Web API
- **Entity Framework Core** (SQL Server provider) for data access
- **JWT Bearer** authentication (`Microsoft.AspNetCore.Authentication.JwtBearer`)
- **Swagger / OpenAPI** (Swashbuckle) for API documentation

## Project structure

```
src/CatalogStore/
└── CatalogStore.BackendAPI/
    ├── Controllers/   # API endpoints
    ├── Data/           # EF Core DbContext
    ├── DTO/            # Data transfer objects
    ├── Models/         # Domain entities
    ├── Repository/     # Data access layer
    └── Services/       # Business logic
```

## Planned modules

- User management (login, roles & permissions)
- Products
- Inventory
- Clients
- Proforma
- Invoicing (facturación)
- Reports & dashboards
- Site content administration
- REST API for future integrations

## Getting started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- SQL Server (Express or higher)

### Run locally

```bash
cd src/CatalogStore/CatalogStore.BackendAPI
dotnet restore
dotnet run
```

The API exposes an OpenAPI/Swagger UI in the `Development` environment.

## License

See [LICENSE](LICENSE).
