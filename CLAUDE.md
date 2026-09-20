# Employee Management API — project context

REST API for managing employees, including CSV bulk import.
Prefer simple, well-justified solutions: the reasoning behind a choice matters more than the volume of code.

## Scope
- Employee fields: Id (system-generated), Name, HireDate, Email, PhoneNo, ProfilePicture (URL), Status (active/inactive),
  Address, State, Country, City, Pincode, CreatedAt.
- Endpoints: `POST /employee`, `GET /employee/{id}`, `GET /employees`, `PUT /employee/{id}`, `DELETE /employee/{id}`,
  `POST /employees/bulk` (CSV import; sample file `data/employees_sample.csv`).
- Business rules: unique email, HireDate not in the future and not before 1900-01-01, E.164 phone, absolute http/https
  profile picture URL, no control characters in Name, Pincode 3–10 letters/digits/spaces/hyphens.
- Invalid input is rejected, never silently corrected; errors are ProblemDetails with 400/404/409/413.

## Architecture
Clean Architecture, dependencies point inwards:
- `src/EmployeeManagement.Domain` — entities (`Employees/Employee.cs`), no dependencies.
- `src/EmployeeManagement.Application` — use cases, validation, repository interfaces. Depends on Domain only.
- `src/EmployeeManagement.Infrastructure` — EF Core / SQL Server (`Persistence/`), CSV parsing (`Csv/`). Implements Application interfaces.
- `src/EmployeeManagement.Api` — ASP.NET Core controllers, composition root (`Program.cs`).
- `tests/EmployeeManagement.UnitTests` — Domain + Application + CSV parser, no DB.
- `tests/EmployeeManagement.IntegrationTests` — `ApiFactory` (WebApplicationFactory + Testcontainers SQL Server, migrations applied).

## Decisions made
- .NET 9 + SQL Server 2022 (Docker Compose), EF Core; only LINQ queries (parameterized) — no raw SQL.
- Ids are random `Guid.NewGuid()` (v4): they appear in URLs, so they must not be guessable.
  Guid v7 was rejected: SQL Server sorts `uniqueidentifier` by its last 6 bytes first, while v7 keeps the timestamp in the
  first bytes, so v7 is NOT sequential in a SQL Server index.
- Random GUIDs as a clustered key cause page splits, so `PK_Employees` is NON-clustered and the table is clustered on
  `CreatedAt` (`IX_Employees_CreatedAt`, migration `ClusterEmployeesByCreatedAt`).
- `Pincode` is a string (leading zeros, e.g. `02101`). `HireDate` is `DateOnly`, `CreatedAt` is `DateTimeOffset`, set by the system.
- Unique index on `Email` enforced in the DB (app-level check only gives a friendly error). SQL Server default collation is case-insensitive.
- `Status` stored as string with a CHECK constraint.
- `TimeProvider` injected for testable "not in the future" checks.
- Swagger UI via Swashbuckle (not the built-in `AddOpenApi`): .NET 9's OpenAPI cannot read XML comments (added in .NET 10),
  and Swashbuckle also ships the UI. `GenerateDocumentationFile` + `NoWarn 1591` (comments only where they add information).
- `TreatWarningsAsErrors` + shared settings in `Directory.Build.props`; package versions pinned in `Directory.Packages.props` (central package management).
- Migrations auto-apply on startup only in Development; tests use environment `Testing`.
- No password is committed anywhere. `.env` (git-ignored) is the single source of `MSSQL_SA_PASSWORD`; docker compose
  uses it for the DB and builds the API's `ConnectionStrings__Default` from it. Missing value → compose fails with a clear `:?` message.
  `InfrastructureServiceCollectionExtensions` fails fast if the connection string is missing.
- API image: multi-stage `Dockerfile` at repo root (context = root for `Directory.*.props`/`global.json`), runs as non-root `$APP_UID`, port 8080.

## CRUD design
- `EmployeeService` (Application) is the reusable path: `Normalize()` → `ValidateAndThrowAsync` → `EmailExistsAsync` → save.
  Bulk import reuses the same pieces (Normalize, validator, mapping) but batches the email check and the save.
- `EmployeeRequest` has all-string nullable fields (HireDate/Status as string → readable per-field errors, same validator for JSON and CSV).
- All mapping lives in `Application/Employees/EmployeeMapping` (`ToResponse`, `ToEmployee`, `ApplyTo`), covered by tests that
  give every field a distinct value, so swapping two fields of the same type fails.
- Errors are exceptions (`ValidationException` from FluentValidation, `NotFoundException`, `ConflictException`) mapped by
  `Api/ErrorHandling/ApiExceptionHandler` to ProblemDetails; the repository maps SQL errors 2601/2627 to `ConflictException` (race on email).
- Field length constants live in `Domain/Employees/EmployeeConstraints` (used by validator and EF mapping). Pincode: column 20, validator 3–10.
- `PUT` checks 404 before validating the body. `GET /employees`: page ≤ 1_000_000 (overflow guard), pageSize ≤ 100, search over Name/Email/City.

## Bulk import design
- Partial import with a per-row report, always 200 when the file structure is valid (no 207). All-or-nothing rejected
  (sample file would import 0/10); a mode parameter rejected as extra code → README "with more time".
- File errors → whole request 400 (`ValidationException` keyed `file`, `EmployeeImportFileError`): missing columns, empty,
  0 rows, > `EmployeeService.MaxImportRows` (1000), malformed CSV, invalid UTF-8. > 1 MB → 413 via `RejectOversizedRequestAttribute`
  (rejects only a known Content-Length over the limit; plain `[RequestSizeLimit]` hit during model binding yields a generic 400).
- Row errors → only that row rejected: validation, duplicate in file (first *valid* occurrence wins), email already in DB
  (one `GetExistingEmailsAsync` query, EF sends the list as one OPENJSON parameter).
- Valid rows saved in one `SaveChangesAsync` (one transaction, no explicit one → no clash with `EnableRetryOnFailure`).
  Re-import is safe (email = idempotency key). Race with `POST /employee` → unique index → 409 for the whole import, client retries.
- `EmployeeCsvReader` (Infrastructure, CsvHelper) only checks structure and returns raw strings; `Row` = file line (header = 1).
  Reads at most MaxImportRows + 1 rows. Error messages are our own (CsvHelper's echo raw content).

## CSV data pitfalls (`data/employees_sample.csv`)
- Future HireDate: Sarah Johnson (2079), Jennifer Lee (2081) → reject.
- Invalid date `2019-13-12` (David Anderson) → format error.
- `1899-02-28` (Amanda Thomas) → rejected by the rule "HireDate ≥ 1900-01-01".
- `CreatedAt` column in CSV is ignored — system sets it.
- Phones like `+1-555-0101` are not strict E.164 → decided: strip spaces/`-`/`(`/`)` first, then validate `^\+[1-9]\d{7,14}$`.
- Sample import result: 6 imported, 4 rejected (file lines 3, 8, 9, 11, all on `hireDate`).

## Commands
- Clean clone: `cp .env.example .env`, set password, `docker compose up --build` → API on http://localhost:8080.
  `/` redirects to Swagger UI (`/swagger`); the OpenAPI document is at `/swagger/v1/swagger.json`. Both are Development-only.
- Local dev: `docker compose up db` + `dotnet run --project src/EmployeeManagement.Api`; connection string from user-secrets
  `ConnectionStrings:Default` (`Database=EmployeeManagement`, same password as `.env`).
- Migrations: `dotnet ef migrations add <Name> -p src/EmployeeManagement.Infrastructure -s src/EmployeeManagement.Api -o Persistence/Migrations`
  (`dotnet tool restore` first — tool pinned in `.config/dotnet-tools.json`).
- Tests: `dotnet test` (Docker must be running for integration tests). Unit tests mock `IEmployeeRepository` with NSubstitute
  (shared setup in the test class constructor); DB behaviour (unique email, collation, transactions) is tested only in integration tests.

## Workflow
- Git operations (branches, commits, push, PRs) are run by the maintainer; only suggest names:
  branches `feat/…`, `fix/…`, `refactor/…`, `test/…`, `docs/…`, `chore/…`; commits in Conventional Commits format.

## Status
Done: project structure, domain model and persistence; CRUD endpoints with validation and ProblemDetails error handling;
CSV bulk import with per-row report; unit and integration tests; Swagger UI with XML comments; README.

Known gaps, deliberately out of scope (listed in the README under "with more time"): no authentication, no optimistic
concurrency on update (last write wins), no API versioning, no structured logging of import outcomes and conflicts,
no health checks (a custom `HealthController` was removed in favour of the built-in `AddHealthChecks` + `AddDbContextCheck`
when needed), test coverage trimmed to the important cases.
