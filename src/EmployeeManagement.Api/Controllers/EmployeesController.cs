using EmployeeManagement.Application.Employees;
using Microsoft.AspNetCore.Mvc;

namespace EmployeeManagement.Api.Controllers;

[ApiController]
public class EmployeesController(EmployeeService service) : ControllerBase
{
    [HttpPost("employee")]
    [ProducesResponseType<EmployeeResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(EmployeeRequest request, CancellationToken ct)
    {
        var employee = await service.CreateAsync(request, ct);

        return CreatedAtAction(nameof(Get), new { id = employee.Id }, employee);
    }

    [HttpGet("employee/{id:guid}")]
    [ProducesResponseType<EmployeeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeResponse>> Get(Guid id, CancellationToken ct) =>
        await service.GetAsync(id, ct);

    [HttpGet("employees")]
    [ProducesResponseType<PagedResult<EmployeeResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<EmployeeResponse>>> List(
        [FromQuery] EmployeeListQuery query, CancellationToken ct) =>
        await service.ListAsync(query, ct);

    [HttpPut("employee/{id:guid}")]
    [ProducesResponseType<EmployeeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EmployeeResponse>> Update(Guid id, EmployeeRequest request, CancellationToken ct) =>
        await service.UpdateAsync(id, request, ct);

    [HttpDelete("employee/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);

        return NoContent();
    }
}
