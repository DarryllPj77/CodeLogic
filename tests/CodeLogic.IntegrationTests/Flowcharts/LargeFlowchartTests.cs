using CodeLogic.Application.Analysis;
using CodeLogic.Infrastructure.Analysis;
using CodeLogic.Infrastructure.Flowcharts;

namespace CodeLogic.IntegrationTests.Flowcharts;

/// <summary>Large graphs stay readable: labels wrap to at most three short lines.</summary>
public sealed class LargeFlowchartTests
{
    /// <summary>A long classic program with loops, a switch, an if-chain, and two methods (40+ nodes).</summary>
    public static readonly string BattleProgram = ProgramTemplates.Classic(
        """
        int[] hps = { 120, 80, 45 };
        int[] mps = { 60, 20, 75 };
        int[] xps = { 1500, 300, 2200 };
        string[] names = { "Knight", "Rogue", "Mage" };
        int round = 1;
        Console.WriteLine("Hello World! The battle begins.");

        while (round <= 3)
        {
            Console.WriteLine($"Round {round}");
            for (int i = 0; i < names.Length; i++)
            {
                if (hps[i] <= 0)
                {
                    continue;
                }

                Act(i, hps, mps, xps, names);
            }

            Report(round, hps);
            round++;
        }

        int survivors = 0;
        foreach (int hp in hps)
        {
            if (hp > 0)
            {
                survivors++;
            }
        }

        Console.WriteLine($"Survivors: {survivors}");
        """,
        """
        static void Act(int i, int[] hps, int[] mps, int[] xps, string[] names)
        {
            switch (i)
            {
                case 0:
                    hps[i] -= 25;
                    Console.WriteLine($"{names[i]} attacks");
                    break;
                case 1:
                    mps[i] += 10;
                    Console.WriteLine($"{names[i]} sneaks");
                    break;
                default:
                    if (mps[i] >= 50 && xps[i] > 1000 && hps[i] > 10)
                    {
                        Console.WriteLine($"{names[i]} casts a very long and dramatic spell");
                        mps[i] -= 50;
                    }
                    else if (mps[i] >= 10)
                    {
                        mps[i] -= 10;
                    }
                    else
                    {
                        Console.WriteLine($"{names[i]} rests");
                    }

                    break;
            }
        }

        static void Report(int round, int[] hps)
        {
            Console.Write($"After round {round}: ");
            for (int i = 0; i < hps.Length; i++)
            {
                Console.Write($"{hps[i]} ");
            }

            Console.WriteLine();
        }
        """);

    private readonly RoslynFlowchartTranslator translator = new(new RoslynCodeAnalysisService());
    private readonly MermaidFlowchartSerializer serializer = new();

    [Fact]
    public async Task TheBattleProgramIsALargeGraphWhoseLabelsAllFitThreeShortLines()
    {
        var translation = await translator.TranslateAsync(BattleProgram);

        Assert.True(translation.IsSuccess, string.Join(" | ", translation.Diagnostics.Select(item => item.Message)));
        var graph = translation.Graph!;
        Assert.True(graph.Nodes.Count >= 42, $"expected at least 42 nodes, got {graph.Nodes.Count}");
        // More than the old Mermaid edge limit of 50, which used to refuse graphs of this size.
        Assert.True(graph.Edges.Count > 50, $"expected more than 50 edges, got {graph.Edges.Count}");

        var document = serializer.Serialize(graph);
        foreach (var node in document.SourceMap)
        {
            var lines = MermaidFlowchartSerializer.WrapLabel(node.Label);
            Assert.InRange(lines.Count, 1, MermaidFlowchartSerializer.MaximumLabelLines);
            Assert.All(lines, line => Assert.True(line.Length <= MermaidFlowchartSerializer.MaximumLabelLineLength, $"'{line}' is too long"));
        }
    }

    [Fact]
    public void ALongConditionWrapsAtItsOperatorsInsteadOfOverflowing()
    {
        var lines = MermaidFlowchartSerializer.WrapLabel("mps[i] ≥ 50 ∧ xps[i] > 1000 ∧ hps[i] > 10");

        Assert.Equal(["mps[i] ≥ 50", "∧ xps[i] > 1000", "∧ hps[i] > 10"], lines);
    }

    [Fact]
    public void TextBeyondThreeLinesEndsWithAnEllipsis()
    {
        var label = "Console.WriteLine($\"{names[i]} casts a very long and dramatic spell that keeps going and going until the whole party has fallen asleep\");";

        var lines = MermaidFlowchartSerializer.WrapLabel(label);

        Assert.Equal(3, lines.Count);
        Assert.EndsWith("…", lines[^1], StringComparison.Ordinal);
        Assert.All(lines, line => Assert.True(line.Length <= MermaidFlowchartSerializer.MaximumLabelLineLength));
    }

    [Fact]
    public void ShortLabelsStayOnOneLine()
    {
        Assert.Equal(["int round = 1;"], MermaidFlowchartSerializer.WrapLabel("int round = 1;"));
    }

    [Fact]
    public async Task WrappedLabelsUseLineBreaksWhileTheSourceMapKeepsTheFullText()
    {
        var source = ProgramTemplates.Classic("""
            int[] mps = { 60 };
            int[] xps = { 1500 };
            int i = 0;
            if (mps[i] >= 50 && xps[i] > 1000 && i == 0)
            {
                Console.WriteLine("Spell");
            }
            """);

        var graph = (await translator.TranslateAsync(source)).Graph!;
        var document = serializer.Serialize(graph);

        Assert.Contains("{\"mps[i] ≥ 50<br/>∧ xps[i] › 1000<br/>∧ i == 0\"}", document.Definition, StringComparison.Ordinal);
        Assert.Contains(document.SourceMap, item => item.Label == "mps[i] >= 50 && xps[i] > 1000 && i == 0");
    }
}
