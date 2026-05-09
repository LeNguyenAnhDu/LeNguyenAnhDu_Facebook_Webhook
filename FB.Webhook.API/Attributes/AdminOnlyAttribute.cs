using System;
using FB.Webhook.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace FB.Webhook.API.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class AdminOnlyAttribute : Attribute, IAuthorizationFilter
{
    private const string AdminHeaderName = "X-Admin-Token";
    private const string ExpectedToken = "admin_secret_2026"; // Mô phỏng một secret key

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        if (!context.HttpContext.Request.Headers.TryGetValue(AdminHeaderName, out var extractedToken))
        {
            context.Result = new UnauthorizedObjectResult(ApiResponse<object>.Fail("Missing X-Admin-Token header", "UNAUTHORIZED"));
            return;
        }

        if (!string.Equals(extractedToken, ExpectedToken, StringComparison.Ordinal))
        {
            context.Result = new UnauthorizedObjectResult(ApiResponse<object>.Fail("Invalid Admin Token", "FORBIDDEN"));
            return;
        }
    }
}
