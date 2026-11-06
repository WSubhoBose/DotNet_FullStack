# Brewery API

An ASP.NET Core API that searches Open Brewery DB and maintains a local SQLite brewery catalog. The solution is split into API, application, domain, and infrastructure projects, with unit and integration tests in `tests/BreweryApi.Tests`.

## Run

From the repository root:

```powershell
dotnet run --project .\src\BreweryApi\BreweryApi.csproj
dotnet test .\tests\BreweryApi.Tests\BreweryApi.Tests.csproj
```

Swagger UI is available at `/swagger`, and the health endpoint is `/health`.

## Brewery endpoints

The local API accepts brewery filters as query parameters:

- `GET /api/v1/breweries` — public, upstream-backed search (deprecated).
- `GET /api/v2/breweries` — authenticated search against the local SQLite catalog.
- `GET /api/v1/breweries/autocomplete` and `GET /api/v2/breweries/autocomplete` — autocomplete; v2 requires the `autocomplete` permission.

Search endpoints support `search`, `name`, `city`, `sortBy`, `sortDirection`, `page`, `pageSize`, `latitude`, and `longitude`. Distance sorting requires both coordinates. Autocomplete accepts `term` and `limit`. See the `.http` files in `src/BreweryApi` for examples.

## Open Brewery DB

The upstream integration follows the [Open Brewery DB breweries endpoint documentation](https://api.openbrewerydb.org/docs/#endpoints-GETv1-breweries). Brewery filters and pagination are sent as JSON body parameters on a `GET /v1/breweries` request; they are not added to the URL query string. For example:

```http
GET https://api.openbrewerydb.org/v1/breweries
Accept: application/json
Content-Type: application/json

{
  "page": 1,
  "per_page": 20,
  "by_city": "denver"
}
```

## Persistence

SQLite persistence uses EF Core. The brewery catalog and authentication data are stored separately; paths can be configured with `Data:SqlitePath` and `Auth:DatabasePath` in `appsettings.json`. Missing databases and tables are initialized when the API starts or first accesses the catalog. The default development users are `user1` (autocomplete enabled) and `user2` (disabled).
