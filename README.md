# Brewery training workspace

This folder contains the Brewery API, its Ocelot gateway, and the Angular app. The API and gateway are separate .NET solutions; the Angular workspace calls the gateway.

## What is here

- [Brewery API](<./.Net Api/BreweryApi.slnx>) — versioned brewery endpoints, token sign-in, and API tests and serves Swagger at `http://localhost:5002/swagger`.
- [Gateway](<./.Net Gateway/Brewery.Gateway.slnx>) — routes requests to the API and serves Swagger at `http://localhost:5001/swagger`.
- [Angular app](<./Angular/Brewery.Ui.Angular>) — login and brewery search screens.

The Angular development server listens on port `4200`.

## Architecture and design

The app is split into three projects with a clear request path: the Angular app sends requests to the Ocelot gateway, and the gateway forwards them to the Brewery API. The API then serves the request from its local SQLite catalog or, for the older v1 endpoints, from the Open Brewery DB service.

```text
Angular UI (4200)
      |
      v
Ocelot gateway (5001) -- routing, CORS, gateway auth, rate limits, Swagger
      |
      v
Brewery API (5002) -- versioned REST endpoints, API auth, validation
      |                                  |
      v                                  v
SQLite catalog and token data       Open Brewery DB
                                    (v1 and catalog refresh)
```

### Brewery API

The API uses a layered design. The Domain project holds the core models, contracts, and interfaces; Application contains brewery and token workflows; Infrastructure implements external HTTP access, caching, and SQLite storage; and the API project exposes the versioned HTTP endpoints and composes the services. The layers keep endpoint code separate from most of the data and integration work.

Several familiar patterns show up in the implementation:

- **Dependency injection and interface-based design** let the API wire application services and infrastructure adapters without controllers needing to know their concrete implementations.
- **Adapter and mapping patterns** keep the Open Brewery DB client and its response models separate from the API's brewery models.
- **Caching with request coalescing** avoids repeating the same upstream request when identical cache misses arrive at once.
- **Resilience policies** add retries with backoff and jitter, plus a circuit breaker, to calls to the external brewery service.
- **Middleware and authentication handlers** centralize exception handling, request logging, rate limiting, and token validation outside of individual endpoints.

The API exposes two generations of brewery endpoints: v1 reads from the external service and is deprecated; v2 uses the SQLite catalog and requires authentication. Autocomplete on v2 also checks the user's permission.

### Gateway

The gateway is a reverse proxy rather than a second implementation of the API. Ocelot's configuration defines which requests are forwarded, and the gateway adds cross-cutting concerns such as CORS, route-level rate limits, and authentication for protected v2 routes. SwaggerForOcelot combines the downstream API's documentation for the gateway UI.

### Angular app

The UI is a standalone Angular application. Its root component handles the screens and user interactions, while `AuthService` and `BreweryApiService` keep authentication and HTTP calls out of the view code. An HTTP interceptor adds the access token to requests, and RxJS observables carry the asynchronous sign-in and search results back to the component.

## Start the services

Install the .NET 9 SDK, Node.js 20.11 or newer, and npm.

Start the API from this folder:

```powershell
dotnet run --project ".\.Net Api\src\BreweryApi\BreweryApi.csproj"
```

The API creates `data\auth.db` and `data\brewery.db` in the `.Net Api` folder. On its first start, it adds `user1` and `user2` to the `Users` table in the auth database.

Start the gateway in another PowerShell terminal. Point it at the same auth database the API created:

```powershell
$apiRoot = (Resolve-Path '.\.Net Api').Path
$env:Auth__DatabasePath = Join-Path $apiRoot 'data\auth.db'
dotnet run --project ".\.Net Gateway\src\BreweryApi.Gateway\BreweryApi.Gateway.csproj"
```

Start Angular in a third terminal:

```powershell
Set-Location .\Angular\Brewery.Ui.Angular
npm install
npm start
```

Open `http://localhost:4200`.

## Sign-in and API access

The sign-in request includes a username and a password field. The API checks that the username exists in SQLite; it does not check, store, or process the password. Enter any string in the password field.

`user1` can use the v2 brewery search and autocomplete endpoints. `user2` can use v2 brewery search, but gets a clear access-denied response from v2 autocomplete. The v1 brewery endpoints are deprecated and allow anonymous access, including through the gateway.

The token endpoint is `POST /api/auth/token`. It issues a short-lived access token and a refresh token for a known username. Send the access token in the `Auth` header when calling protected v2 routes. Refresh tokens can be exchanged at `POST /api/auth/refresh`.

## Tests

Run the API tests from this folder:

```powershell
dotnet test ".\.Net Api\BreweryApi.slnx" --nologo
```

Run Angular unit tests and build:

```powershell
Set-Location .\Angular\Brewery.Ui.Angular
npm run test:unit
npm run build
```

To run the Playwright browser test, install Chromium once:

```powershell
npm exec --workspace=brewery-ui-angular-tests -- playwright install chromium
```

With the API, gateway, and Angular app running, run:

```powershell
npm run test:e2e
```

The browser test signs in as `user1` and uses a placeholder password value.

## Before using outside training

The current sign-in flow treats the password as unused input. It is for this training setup, not a production authentication system. Use a proper identity provider and HTTPS before exposing the services.
