using CodeLogic.Infrastructure.Analysis;

namespace CodeLogic.IntegrationTests.Analysis;

public sealed class RoslynEditorStructureServiceTests
{
    private readonly RoslynEditorStructureService service = new();

    [Fact]
    public async Task FindsRequestedCSharpFoldableBlocks()
    {
        const string source = """
            namespace Demo
            {
                class Program
                {
                    void Run()
                    {
                        if (true)
                        {
                        }
                        else
                        {
                        }
                        for (;;)
                        {
                        }
                        foreach (var item in items)
                        {
                        }
                        while (true)
                        {
                        }
                        switch (value)
                        {
                            default: break;
                        }
                        try
                        {
                        }
                        catch
                        {
                        }
                    }
                }
            }
            """;

        var result = await service.GetFoldRegionsAsync(source);

        var kinds = result.Select(region => region.Kind).ToHashSet(StringComparer.Ordinal);
        Assert.Contains("namespace", kinds);
        Assert.Contains("class", kinds);
        Assert.Contains("method", kinds);
        Assert.Contains("if", kinds);
        Assert.Contains("else", kinds);
        Assert.Contains("for", kinds);
        Assert.Contains("foreach", kinds);
        Assert.Contains("while", kinds);
        Assert.Contains("switch", kinds);
        Assert.Contains("try", kinds);
        Assert.Contains("catch", kinds);
        Assert.All(result, region => Assert.True(region.EndLine > region.StartLine));
    }

    [Fact]
    public async Task SingleLineBlocksAreNotFoldableAndRegionsAreOrderedOuterFirst()
    {
        const string source = """
            if (ready) { Console.WriteLine("one line"); }
            while (count < 3)
            {
                if (count == 1)
                {
                    count++;
                }
            }
            """;

        var result = await service.GetFoldRegionsAsync(source);

        Assert.Collection(
            result,
            outer =>
            {
                Assert.Equal((3, 8, "while"), (outer.StartLine, outer.EndLine, outer.Kind));
                Assert.Equal(5, outer.HiddenLineCount);
            },
            inner => Assert.Equal((5, 7, "if"), (inner.StartLine, inner.EndLine, inner.Kind)));
    }

    [Fact]
    public async Task EmptyAndOversizedSourceReturnNoRanges()
    {
        Assert.Empty(await service.GetFoldRegionsAsync(null));
        Assert.Empty(await service.GetFoldRegionsAsync(" "));
        Assert.Empty(await service.GetFoldRegionsAsync(new string('x', 10_001)));
    }
}
