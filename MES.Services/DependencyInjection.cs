using Microsoft.Extensions.DependencyInjection;

namespace MES.Services;

public static class DependencyInjection
{
public static IServiceCollection AddServices(this IServiceCollection services)
{
    services.AddScoped<IAuthService, AuthService>();
    services.AddScoped<IInitializationService, InitializationService>();
    return services;
}
}
