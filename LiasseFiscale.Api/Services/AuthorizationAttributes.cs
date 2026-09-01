using System.IdentityModel.Tokens.Jwt;
using LiasseFiscale.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace LiasseFiscale.Api.Services;

/// <summary>
/// Custom authorization filter to verify user has access to a specific company.
/// Use on controller actions that require company-level authorization.
/// </summary>
public class AuthorizeForCompanyAttribute : Attribute, IAsyncActionFilter
{
    private readonly string _contribuableIdParamName;

    /// <summary>
    /// Create authorization filter.
    /// </summary>
    /// <param name="contribuableIdParamName">Name of route/query parameter containing the company ID</param>
    public AuthorizeForCompanyAttribute(string contribuableIdParamName = "contribuableId")
    {
        _contribuableIdParamName = contribuableIdParamName;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // Get user ID from JWT claim
        var userIdClaim = context.HttpContext.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (!int.TryParse(userIdClaim, out var userId))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        // Get company ID from request
        if (!context.ActionArguments.TryGetValue(_contribuableIdParamName, out var companyIdObj) ||
            !int.TryParse(companyIdObj?.ToString(), out var contribuableId))
        {
            context.Result = new BadRequestObjectResult(new { message = "Company ID required." });
            return;
        }

        // Check authorization
        var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
        var isAuthorized = await db.UserCompanyAuthorizations
            .Where(a => a.UserId == userId && a.ContribuableId == contribuableId && a.IsActive &&
                       (a.DateExpired == null || a.DateExpired > DateTime.UtcNow))
            .AnyAsync();

        if (!isAuthorized)
        {
            context.Result = new ForbidResult();
            return;
        }

        await next();
    }
}

/// <summary>
/// Helper extensions for extracting user information from HTTP context.
/// </summary>
public static class AuthorizationExtensions
{
    public static int GetUserId(this HttpContext context)
    {
        var userIdClaim = context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        return int.TryParse(userIdClaim, out var userId) ? userId : 0;
    }

    public static string? GetUserEmail(this HttpContext context)
    {
        return context.User.FindFirst(JwtRegisteredClaimNames.Email)?.Value;
    }

    public static string? GetUserIpAddress(this HttpContext context)
    {
        return context.Connection.RemoteIpAddress?.ToString();
    }

    public static string? GetUserAgent(this HttpContext context)
    {
        return context.Request.Headers["User-Agent"].ToString();
    }
}
