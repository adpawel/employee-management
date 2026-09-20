using EmployeeManagement.Api.ErrorHandling;
using EmployeeManagement.Application;
using EmployeeManagement.Infrastructure;
using EmployeeManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Employee Management API",
        Version = "v1",
        Description = "CRUD for employees plus CSV bulk import with a per-row report."
    });

    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "EmployeeManagement.Api.xml"));
});

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "Employee Management API v1"));
    app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

    // Applying migrations on startup keeps local setup to "docker compose up + dotnet run".
    // Outside Development, schema changes should be an explicit deployment step instead.
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

app.UseHttpsRedirection();

app.MapControllers();

await app.RunAsync();

public partial class Program { }
