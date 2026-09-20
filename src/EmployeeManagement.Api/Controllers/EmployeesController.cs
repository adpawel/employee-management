using EmployeeManagement.Api.ErrorHandling;
using EmployeeManagement.Application.Employees;
using Microsoft.AspNetCore.Mvc;

namespace EmployeeManagement.Api.Controllers;

[ApiController]
public class EmployeesController(EmployeeService service, IEmployeeCsvReader csvReader) : ControllerBase
{
    private const int MaxImportFileBytes = 1_048_576;

    /// <summary>Creates an employee.</summary>
    /// <remarks>Id and CreatedAt are assigned by the system; values sent by the client are ignored.</remarks>
    /// <response code="201">Created; the Location header points to the new employee.</response>
    /// <response code="400">Validation failed; errors are reported per field.</response>
    /// <response code="409">Another employee already uses this email.</response>
    [HttpPost("employee")]
    [ProducesResponseType<EmployeeResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(EmployeeRequest request, CancellationToken ct)
    {
        var employee = await service.CreateAsync(request, ct);

        return CreatedAtAction(nameof(Get), new { id = employee.Id }, employee);
    }

    /// <summary>Returns a single employee.</summary>
    /// <response code="200">The employee.</response>
    /// <response code="404">No employee with this id.</response>
    [HttpGet("employee/{id:guid}")]
    [ProducesResponseType<EmployeeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeResponse>> Get(Guid id, CancellationToken ct) =>
        await service.GetAsync(id, ct);

    /// <summary>Returns a page of employees, optionally filtered by a search term.</summary>
    /// <remarks>The search term is matched against name, email and city. Page size is capped at 100.</remarks>
    /// <response code="200">The requested page.</response>
    /// <response code="400">Invalid paging or a search term longer than 100 characters.</response>
    [HttpGet("employees")]
    [ProducesResponseType<PagedResult<EmployeeResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<EmployeeResponse>>> List(
        [FromQuery] EmployeeListQuery query, CancellationToken ct) =>
        await service.ListAsync(query, ct);

    /// <summary>Replaces an employee.</summary>
    /// <remarks>A full replacement: every editable field must be sent. Id and CreatedAt never change.</remarks>
    /// <response code="200">The updated employee.</response>
    /// <response code="400">Validation failed; errors are reported per field.</response>
    /// <response code="404">No employee with this id.</response>
    /// <response code="409">Another employee already uses this email.</response>
    [HttpPut("employee/{id:guid}")]
    [ProducesResponseType<EmployeeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EmployeeResponse>> Update(Guid id, EmployeeRequest request, CancellationToken ct) =>
        await service.UpdateAsync(id, request, ct);

    // Content-Type and file extension are not trusted: the file is accepted only if it parses as the expected CSV.
    /// <summary>Imports employees from a CSV file.</summary>
    /// <remarks>
    /// The import is partial: an invalid row is rejected and reported, the remaining rows are still imported.
    /// All imported rows are saved in one transaction, so a database failure never leaves half an import behind.
    /// Re-importing the same file is safe: rows already in the database come back as rejected duplicates.
    /// A problem with the file itself (missing columns, malformed CSV, no rows, more than 1000 rows) rejects
    /// the whole request instead. The file extension and Content-Type are not trusted; parsing decides.
    /// </remarks>
    /// <response code="200">The import finished; the report says which rows were imported and why the others were not.</response>
    /// <response code="400">The file is missing or cannot be read as the expected CSV.</response>
    /// <response code="409">A concurrent request took one of the emails; retry the import.</response>
    /// <response code="413">The request is larger than 1 MB.</response>
    [HttpPost("employees/bulk")]
    [Consumes("multipart/form-data")]
    [RejectOversizedRequest(MaxImportFileBytes)]
    [RequestSizeLimit(MaxImportFileBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxImportFileBytes)]
    [ProducesResponseType<EmployeeImportResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    public async Task<ActionResult<EmployeeImportResult>> Import(IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        var rows = await csvReader.ReadAsync(stream, ct);

        return await service.ImportAsync(rows, ct);
    }

    /// <summary>Deletes an employee.</summary>
    /// <response code="204">Deleted.</response>
    /// <response code="404">No employee with this id.</response>
    [HttpDelete("employee/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);

        return NoContent();
    }
}
