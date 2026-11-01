using CodeLogic.Infrastructure.Analysis;
using CodeLogic.Infrastructure.Identity;
using CodeLogic.Infrastructure.Persistence;
using CodeLogic.Web.Components;
using CodeLogic.Web.Identity;
using CodeLogic.Web.Observability;
using CodeLogic.Web.Progress;
using CodeLogic.Web.Security;

using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthorization();
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = SecurityRequestLimits.MaximumRequestBodyBytes;
    options.ValueLengthLimit = SecurityRequestLimits.MaximumFormValueBytes;
    options.ValueCountLimit = SecurityRequestLimits.MaximumFormValueCount;
});
builder.WebHost.ConfigureKestrel(options =>
    options.Limits.MaxRequestBodySize = SecurityRequestLimits.MaximumRequestBodyBytes);
builder.Services.AddCodeLogicAnalysis();
builder.Services.AddCodeLogicPersistence(
    builder.Configuration.GetConnectionString("CodeLogic")
        ?? throw new InvalidOperationException("Connection string 'CodeLogic' is required."),
    builder.Environment.ContentRootPath);
builder.Services.AddIdentity<CodeLogicUser, IdentityRole>(options =>
{
    options.Password.RequiredLength = 12;
    options.Password.RequiredUniqueChars = 1;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedAccount = false;
})
    .AddEntityFrameworkStores<CodeLogicDbContext>()
    .AddDefaultTokenProviders();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "CodeLogic.Identity";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.LoginPath = "/account/login";
    options.AccessDeniedPath = "/account/access-denied";
    options.ExpireTimeSpan = TimeSpan.FromHours(2);
    options.SlidingExpiration = true;
});
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseReadinessHealthCheck>("database", tags: ["ready"]);

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseCodeLogicCorrelationId();
app.UseCodeLogicSecurityHeaders();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.MapIdentityEndpoints();
app.MapQuizSubmission();
app.MapHealthChecks(
    "/health/live",
    new HealthCheckOptions
    {
        Predicate = static _ => false,
    });
app.MapHealthChecks(
    "/health/ready",
    new HealthCheckOptions
    {
        Predicate = static registration => registration.Tags.Contains("ready"),
    });

app.Run();
