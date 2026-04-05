using Microsoft.Extensions.DependencyInjection;
using NewDialer.Application;
using NewDialer.Application.Abstractions;
using NewDialer.Infrastructure.Activity;
using NewDialer.Infrastructure.Agents;
using NewDialer.Infrastructure.Auth;
using NewDialer.Infrastructure.Leads;
using NewDialer.Infrastructure.Platform;
using NewDialer.Infrastructure.Services;
using NewDialer.Infrastructure.Spreadsheets;

namespace NewDialer.Infrastructure.Composition;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNewDialerCoreServices(this IServiceCollection services)
    {
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddScoped<ISubscriptionAccessEvaluator, SubscriptionAccessEvaluator>();
        services.AddScoped<IAgentActivityService, AgentActivityService>();
        services.AddScoped<IAgentManagementService, AgentManagementService>();
        services.AddScoped<ILeadSpreadsheetService, ClosedXmlLeadSpreadsheetService>();
        services.AddScoped<ILeadManagementService, LeadManagementService>();
        services.AddScoped<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddScoped<IWorkspaceKeyGenerator, WorkspaceKeyGenerator>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<IPlatformOverviewService, PlatformOverviewService>();
        return services;
    }
}
