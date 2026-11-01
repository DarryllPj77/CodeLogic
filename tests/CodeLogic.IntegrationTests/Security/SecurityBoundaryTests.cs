using CodeLogic.Application.Analysis;
using CodeLogic.Application.Execution;
using CodeLogic.Infrastructure.Analysis;
using CodeLogic.Infrastructure.Execution;
using CodeLogic.Infrastructure.Flowcharts;
using CodeLogic.Web.Identity;
using CodeLogic.Web.Security;

using Microsoft.AspNetCore.Http;

namespace CodeLogic.IntegrationTests.Security;

public sealed class SecurityBoundaryTests
{
    [Fact]
    public async Task SecurityMiddlewareAddsBrowserHardeningHeaders()
    {
        var nextCalled = false;
        var middleware = new SecurityHeadersMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = new DefaultHttpContext();

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
        Assert.Equal("same-origin", context.Response.Headers["Cross-Origin-Opener-Policy"]);
        Assert.Contains("camera=()", context.Response.Headers["Permissions-Policy"].ToString(), StringComparison.Ordinal);
        Assert.Equal("no-referrer", context.Response.Headers["Referrer-Policy"]);
        Assert.Equal("nosniff", context.Response.Headers["X-Content-Type-Options"]);
        Assert.Equal("DENY", context.Response.Headers["X-Frame-Options"]);
        Assert.Equal("none", context.Response.Headers["X-Permitted-Cross-Domain-Policies"]);
    }

    [Theory]
    [InlineData(255, 12, 10)]
    [InlineData(20, 129, 10)]
    [InlineData(20, 12, 2049)]
    public void OversizedIdentityInputsAreRejected(int emailLength, int passwordLength, int returnUrlLength)
    {
        var accepted = IdentityInputLimits.AreWithinLimits(
            new string('e', emailLength),
            new string('p', passwordLength),
            "/" + new string('r', returnUrlLength - 1));

        Assert.False(accepted);
    }

    [Fact]
    public async Task DangerousProcessSnippetFailsClosedWithoutTrace()
    {
        const string source = "System.Diagnostics.Process.Start(\"calc\");";
        var analyzer = new RoslynCodeAnalysisService();
        var translator = new RoslynFlowchartTranslator(analyzer);
        var interpreter = new RoslynPedagogicalInterpreter(translator);

        var analysis = await analyzer.AnalyzeAsync(source);
        var trace = await interpreter.TraceAsync(source);

        Assert.Equal(CodeAnalysisStatus.Unsupported, analysis.Status);
        Assert.Equal(ExecutionTraceStatus.Unsupported, trace.Status);
        Assert.Empty(trace.Steps);
        Assert.False(trace.HasTrace);
    }

    [Fact]
    public void ProductionSourceContainsNoArbitraryExecutionPrimitive()
    {
        var sourceRoot = Path.Combine(FindRepositoryRoot(), "src");
        string[] forbiddenTokens =
        [
            "CSharpCompilation",
            "MetadataReference.CreateFromFile",
            "Microsoft.CodeAnalysis.Emit",
            "Process.Start(",
            "Assembly.Load(",
            "AssemblyLoadContext",
        ];

        var findings = ReadProductionSource(sourceRoot)
            .SelectMany(file => forbiddenTokens
                .Where(token => file.Content.Contains(token, StringComparison.Ordinal))
                .Select(token => $"{file.Path}: {token}"))
            .ToArray();

        Assert.Empty(findings);
    }

    [Fact]
    public void WebHasNoFileUploadSurface()
    {
        var webRoot = Path.Combine(FindRepositoryRoot(), "src", "CodeLogic.Web");
        string[] uploadTokens = ["IFormFile", "<InputFile", "type=\"file\"", "MapPost(\"/upload"];
        var findings = ReadProductionSource(webRoot)
            .SelectMany(file => uploadTokens
                .Where(token => file.Content.Contains(token, StringComparison.OrdinalIgnoreCase))
                .Select(token => $"{file.Path}: {token}"))
            .ToArray();

        Assert.Empty(findings);
    }

    [Fact]
    public void StateChangingRazorFormsAndMappedEndpointsKeepAntiforgeryBoundary()
    {
        var webRoot = Path.Combine(FindRepositoryRoot(), "src", "CodeLogic.Web");
        var formFiles = Directory.GetFiles(webRoot, "*.razor", SearchOption.AllDirectories)
            .Select(path => new { Path = path, Content = File.ReadAllText(path) })
            .Where(file => file.Content.Contains("<form", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var program = File.ReadAllText(Path.Combine(webRoot, "Program.cs"));

        Assert.NotEmpty(formFiles);
        Assert.All(
            formFiles,
            file => Assert.Contains("<AntiforgeryToken", file.Content, StringComparison.Ordinal));
        var middlewareIndex = program.IndexOf("app.UseAntiforgery();", StringComparison.Ordinal);
        Assert.True(middlewareIndex > 0);
        Assert.True(middlewareIndex < program.IndexOf("app.MapIdentityEndpoints();", StringComparison.Ordinal));
        Assert.True(middlewareIndex < program.IndexOf("app.MapQuizSubmission();", StringComparison.Ordinal));
    }

    [Fact]
    public void MermaidBrowserRendererKeepsStrictSanitizationAndAvoidsInnerHtml()
    {
        var scriptPath = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "CodeLogic.Web",
            "wwwroot",
            "js",
            "playground.js");
        var script = File.ReadAllText(scriptPath);

        Assert.Contains("securityLevel: \"strict\"", script, StringComparison.Ordinal);
        Assert.Contains("sanitizeSvg(svg)", script, StringComparison.Ordinal);
        Assert.Contains("replaceChildren", script, StringComparison.Ordinal);
        Assert.Contains("script, foreignObject, iframe, object, embed", script, StringComparison.Ordinal);
        Assert.DoesNotContain("innerHTML", script, StringComparison.Ordinal);
    }

    [Fact]
    public void RequestLimitsAreSmallAndExplicit()
    {
        Assert.Equal(65_536, SecurityRequestLimits.MaximumRequestBodyBytes);
        Assert.Equal(16_384, SecurityRequestLimits.MaximumFormValueBytes);
        Assert.Equal(32, SecurityRequestLimits.MaximumFormValueCount);
    }

    private static IEnumerable<(string Path, string Content)> ReadProductionSource(string root) =>
        Directory.GetFiles(root, "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Select(path => (path, File.ReadAllText(path)));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CodeLogic.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate the CodeLogic repository root.");
    }
}
