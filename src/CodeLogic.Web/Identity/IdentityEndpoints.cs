using CodeLogic.Infrastructure.Identity;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CodeLogic.Web.Identity;

public static class IdentityEndpoints
{
    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/account/register", RegisterAsync);
        endpoints.MapPost("/account/login", LoginAsync);
        endpoints.MapPost("/account/logout", LogoutAsync).RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> RegisterAsync(
        [FromForm] RegisterSubmission submission,
        UserManager<CodeLogicUser> userManager,
        SignInManager<CodeLogicUser> signInManager)
    {
        var email = submission.Email?.Trim();
        if (string.IsNullOrWhiteSpace(email)
            || submission.Password != submission.ConfirmPassword)
        {
            return Results.LocalRedirect("/account/register?error=invalid");
        }

        var user = new CodeLogicUser { UserName = email, Email = email };
        var result = await userManager.CreateAsync(user, submission.Password ?? string.Empty);
        if (!result.Succeeded)
        {
            var error = result.Errors.Any(item => item.Code.StartsWith("Password", StringComparison.Ordinal))
                ? "password"
                : "invalid";
            return Results.LocalRedirect($"/account/register?error={error}");
        }

        await signInManager.SignInAsync(user, isPersistent: false);
        return Results.LocalRedirect(GetLocalReturnUrl(submission.ReturnUrl));
    }

    private static async Task<IResult> LoginAsync(
        [FromForm] LoginSubmission submission,
        SignInManager<CodeLogicUser> signInManager)
    {
        var email = submission.Email?.Trim();
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(submission.Password))
        {
            return Results.LocalRedirect(LoginErrorUrl("invalid", submission.ReturnUrl));
        }

        var result = await signInManager.PasswordSignInAsync(
            email,
            submission.Password,
            isPersistent: false,
            lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            var error = result.IsLockedOut ? "locked" : "invalid";
            return Results.LocalRedirect(LoginErrorUrl(error, submission.ReturnUrl));
        }

        return Results.LocalRedirect(GetLocalReturnUrl(submission.ReturnUrl));
    }

    private static async Task<IResult> LogoutAsync(SignInManager<CodeLogicUser> signInManager)
    {
        await signInManager.SignOutAsync();
        return Results.LocalRedirect("/");
    }

    private static string LoginErrorUrl(string error, string? returnUrl) =>
        $"/account/login?error={error}&returnUrl={Uri.EscapeDataString(GetLocalReturnUrl(returnUrl))}";

    internal static string GetLocalReturnUrl(string? returnUrl) =>
        !string.IsNullOrWhiteSpace(returnUrl)
        && returnUrl[0] == '/'
        && !returnUrl.StartsWith("//", StringComparison.Ordinal)
        && !returnUrl.StartsWith("/\\", StringComparison.Ordinal)
            ? returnUrl
            : "/";
}

public sealed class RegisterSubmission
{
    public string? Email { get; set; }

    public string? Password { get; set; }

    public string? ConfirmPassword { get; set; }

    public string? ReturnUrl { get; set; }
}

public sealed class LoginSubmission
{
    public string? Email { get; set; }

    public string? Password { get; set; }

    public string? ReturnUrl { get; set; }
}
