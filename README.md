# Employee Management API

REST API for managing employees, including CSV bulk import.
.NET 9 · ASP.NET Core · EF Core · SQL Server 2022 · xUnit + Testcontainers.

## Run it

Requires Docker.

```bash
cp .env.example .env     # set MSSQL_SA_PASSWORD (any strong password)
docker compose up --build
```

Open <http://localhost:8080> - it redirects to Swagger UI, where every endpoint can be tried out.

Tests (Docker must be running, .NET 9 SDK required):

```bash
dotnet test
```

## Endpoints

| Method | Path | Notes |
|-|-|-|
| POST | `/employee` | 201 + `Location`; 400 validation, 409 duplicate email |
| GET | `/employee/{id}` | 200 / 404 |
| GET | `/employees` | paging (`page`, `pageSize` ≤ 100) and `search` over name, email, city |
| PUT | `/employee/{id}` | full replacement; 200 / 400 / 404 / 409 |
| DELETE | `/employee/{id}` | 204 / 404 |
| POST | `/employees/bulk` | `multipart/form-data`, field `file`; 200 with a per-row report |

Errors use [ProblemDetails](https://datatracker.ietf.org/doc/html/rfc9457) with a `traceId`; validation errors are reported per field.
Ready-made requests for every endpoint: [`src/EmployeeManagement.Api/EmployeeManagement.Api.http`](src/EmployeeManagement.Api/EmployeeManagement.Api.http).

### Sample requests

```bash
# Create
curl -X POST http://localhost:8080/employee -H 'Content-Type: application/json' -d '{
  "name":"Jane Doe","hireDate":"2023-04-17","email":"jane.doe@example.com",
  "phoneNo":"+1 (555) 010-1234","profilePicture":"https://example.com/jane.jpg","status":"Active",
  "address":"1 Main Street","state":"Massachusetts","country":"USA","city":"Boston","pincode":"02101"}'

# List with search and paging
curl 'http://localhost:8080/employees?search=boston&page=1&pageSize=20'

# Bulk import
curl -F "file=@data/employees_sample.csv" http://localhost:8080/employees/bulk
```

Input is normalized before validation but never silently corrected: the phone above is stored as `+15550101234`
(separators stripped, letters are not), the email is lowercased, `"Active"` is stored as `active`, while `"activ"` is rejected.

The provided `data/employees_sample.csv` imports 6 of 10 rows; the other 4 are reported with the reason:

```json
{ "total": 10, "imported": 6, "rejected": 4, "results": [
  { "row": 2, "email": "john.smith@company.com", "status": "imported", "id": "…", "errors": null },
  { "row": 3, "email": "sarah.johnson@company.com", "status": "rejected", "id": null,
    "errors": { "hireDate": ["'Hire Date' must not be in the future."] } }
]}
```

## Technology choices

- **.NET 9 / ASP.NET Core** - the stack of the role and the one I work with daily, and a natural fit for a relational, transactional domain.
- **SQL Server 2022** - relational data with a uniqueness constraint that has to hold under concurrency; also the database I use at work.
- **Docker Compose for the database** - one command reproduces the same environment on any machine, with no local SQL Server install.
- **EF Core, LINQ only** - queries are parameterized by the provider, so no user input is ever concatenated into SQL.
- **FluentValidation** - validation rules live outside the DTO and read like the business rules they encode.
- **xUnit, NSubstitute, Testcontainers** - fast unit tests against a mocked repository, plus integration tests against a real SQL Server in a container.

## Design decisions

**Clean Architecture in four projects (Domain, Application, Infrastructure, Api).** I am used to it and it stays extensible;
four separate projects rather than four folders because then the compiler, not a convention, enforces the direction of dependencies.

**Random GUID (v4) ids.** Ids appear in URLs, so they must not be guessable or enumerable. GUID v7 was rejected:
SQL Server orders `uniqueidentifier` by its last bytes, so a v7 timestamp prefix does not make the index sequential.

**Non-clustered primary key.** Random GUIDs as a clustered key cause page splits, so the table is clustered on `CreatedAt` instead.

**Bulk import is partial, with a per-row report.** Employee rows are independent, so one bad row must not reject the rest -
all-or-nothing would import 0 of the 10 sample rows. The response is always 200 when the file itself is valid because each row is reported independently.

**Two levels of import failures.** A problem with the file (missing column, unreadable CSV, no rows, more than 1000 rows) rejects
the whole request with 400 or 413; a problem with a row (validation, duplicate inside the file, email already in the database)
rejects only that row and is reported with its line number and field errors.

**Business rules beyond the brief.** Besides unique email, hire date not in the future and E.164 phone numbers:
`HireDate` must not be earlier than 1900-01-01 (catches typos and the 1899 row in the sample file); `ProfilePicture` must be an
absolute `http`/`https` URL (`javascript:` and `file:` URLs are rejected, since the value ends up in a browser);
`Name` must not contain control characters; `Pincode` must be 3–10 letters, digits, spaces or hyphens.


## With more time

- **Authentication and authorization** - the endpoints are completely open today; this is the first thing I would add.
- **A more professional bulk import** - a queue with an import job and a status endpoint for large files, or a mode parameter
  to choose all-or-nothing instead of partial import. Neither was warranted at this scale.
- **Logging** - structured logs for import outcomes and conflicts, with the `traceId` already returned in error responses used for correlation.
- **Another pass over test coverage** - the suite covers the important cases deliberately, not exhaustively.

## AI tool usage

I used **Claude Code** as the main tool, and **Gemini** to validate the approach at key decision points.
AI wrote most of the code; I reviewed it and made the decisions that shape how the project works. What helped most was keeping a
project context file with the decisions already made - beyond that, extra tooling would have been overkill at this scale.

Things I changed or rejected:

- **Four projects instead of one project with four folders**, so that dependency direction is checked at compile time.
- **`Pincode` as a string**, against the suggestion to store it as a number - that would have dropped the leading zero in `02101`
  and made non-numeric postal codes impossible.
- **NSubstitute instead of a hand-written fake repository**, which removed a test class we had to maintain ourselves.
- **All mapping moved into one place** (`EmployeeMapping`), plus smaller refactors - mapping had been spread across the service,
  and a test now asserts every field with a distinct value, so swapping two fields of the same type fails the build.
