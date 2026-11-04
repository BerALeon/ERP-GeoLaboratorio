using System.Security.Claims;
using FluentValidation;
using Geolaboratorio.Application;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Geolaboratorio.Api;

public static class Policies
{
    public const string Admin = "Admin";
    public const string AdminOTecnico = "AdminOTecnico";
}

public class CurrentUser(IHttpContextAccessor http) : ICurrentUser
{
    private ClaimsPrincipal? P => http.HttpContext?.User;
    public int? UserId => int.TryParse(P?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    public string? Nombre => P?.FindFirstValue(ClaimTypes.Name);
    public string? Rol => P?.FindFirstValue(ClaimTypes.Role);
    public string? Ip => http.HttpContext?.Connection.RemoteIpAddress?.ToString();
}

/// <summary>Ejecuta automÃ¡ticamente los validadores FluentValidation sobre los DTO de entrada.</summary>
public class ValidationFilter(IServiceProvider sp) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext ctx, ActionExecutionDelegate next)
    {
        foreach (var arg in ctx.ActionArguments.Values.Where(a => a is not null))
        {
            var vt = typeof(IValidator<>).MakeGenericType(arg!.GetType());
            if (sp.GetService(vt) is not IValidator v) continue;
            var res = await v.ValidateAsync(new ValidationContext<object>(arg));
            if (!res.IsValid)
            {
                var errors = res.Errors.GroupBy(e => e.PropertyName).ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
                ctx.Result = new BadRequestObjectResult(new ValidationProblemDetails(errors) { Title = "Datos invÃ¡lidos", Status = 400 });
                return;
            }
        }
        await next();
    }
}

public class ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> log)
{
    public async Task Invoke(HttpContext ctx)
    {
        try { await next(ctx); }
        catch (Exception ex)
        {
            var (status, title) = ex switch
            {
                NotFoundException => (404, ex.Message),
                BusinessException => (409, ex.Message),
                UnauthorizedAccessException => (401, ex.Message),
                ForbiddenException => (403, ex.Message),
                _ => (500, "Error interno del servidor.")
            };
            if (status == 500) log.LogError(ex, "Error no controlado");
            if (ctx.Response.HasStarted) throw;
            ctx.Response.StatusCode = status;
            await ctx.Response.WriteAsJsonAsync(new ProblemDetails { Status = status, Title = title });
        }
    }
}

