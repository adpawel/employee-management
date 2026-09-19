using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EmployeeManagement.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Business rules like "HireDate cannot be in the future" depend on the current time;
        // injecting TimeProvider lets tests substitute a fixed clock.
        services.TryAddSingleton(TimeProvider.System);

        return services;
    }
}
