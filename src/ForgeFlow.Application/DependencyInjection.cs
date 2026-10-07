using ForgeFlow.Application.Approvals;
using ForgeFlow.Application.Audit;
using ForgeFlow.Application.Auth;
using ForgeFlow.Application.Changes;
using ForgeFlow.Application.Components;
using ForgeFlow.Application.Dashboard;
using ForgeFlow.Application.Products;
using ForgeFlow.Application.Search;
using ForgeFlow.Application.Users;
using ForgeFlow.Application.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ForgeFlow.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IComponentService, ComponentService>();
        services.AddScoped<IEngineeringChangeService, EngineeringChangeService>();
        services.AddScoped<IApprovalService, ApprovalService>();
        services.AddScoped<IWorkflowService, WorkflowService>();
        services.AddScoped<IAuditQueryService, AuditQueryService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<ISearchService, SearchService>();
        return services;
    }
}
