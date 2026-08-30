using AutomationStudio.Abstractions;
using AutomationStudio.Core.Vision;

namespace AutomationStudio.Core.Tests.Vision;

/// <summary>Design doc section 26's self-healing signal: "the original selector failed, a fallback succeeded" — pure logic over VisualTarget/TargetResolution, no live resolution needed.</summary>
public class WorkflowHealerTests
{
    [Fact]
    public void Propose_ResolvedStrategyMatchesThePrimaryConfiguredTier_ReturnsNull()
    {
        var target = new VisualTarget(Element: new ElementQuery(Text: "Export"), OcrText: "Export");
        var resolution = new TargetResolution(TargetStrategy.ElementFinder, new ScreenPoint(1, 1), 0.98);

        Assert.Null(WorkflowHealer.Propose(target, resolution));
    }

    [Fact]
    public void Propose_ElementConfiguredButOcrResolvedIt_ProposesHealingFlaggingTheStaleElementTier()
    {
        var target = new VisualTarget(Element: new ElementQuery(Text: "Export"), OcrText: "Export");
        var resolution = new TargetResolution(TargetStrategy.Ocr, new ScreenPoint(10, 20), 0.9);

        var proposal = WorkflowHealer.Propose(target, resolution);

        Assert.NotNull(proposal);
        Assert.Equal(TargetStrategy.ElementFinder, proposal!.FailedStrategy);
        Assert.Equal(TargetStrategy.Ocr, proposal.SucceededStrategy);
    }

    [Fact]
    public void Propose_OnlyCoordinatesActuallySucceeded_CapturesTheResolvedPointIntoTheUpdatedTarget()
    {
        var target = new VisualTarget(Element: new ElementQuery(Text: "Export"), Coordinates: new ScreenPoint(1, 1));
        var resolution = new TargetResolution(TargetStrategy.Coordinates, new ScreenPoint(742, 510), 1.0);

        var proposal = WorkflowHealer.Propose(target, resolution);

        Assert.NotNull(proposal);
        Assert.Equal(TargetStrategy.ElementFinder, proposal!.FailedStrategy);
        Assert.Equal(TargetStrategy.Coordinates, proposal.SucceededStrategy);
        Assert.Equal(new ScreenPoint(742, 510), proposal.UpdatedTarget.Coordinates);
    }

    [Fact]
    public void Propose_OcrIsThePrimaryConfiguredTierAndItSucceeded_ReturnsNull()
    {
        // No Element query at all — OCR IS the highest-priority tier that's configured, so it
        // succeeding is not a fallback.
        var target = new VisualTarget(OcrText: "Export", TemplateImage: [1, 2, 3]);
        var resolution = new TargetResolution(TargetStrategy.Ocr, new ScreenPoint(10, 20), 0.9);

        Assert.Null(WorkflowHealer.Propose(target, resolution));
    }

    [Fact]
    public void Propose_TemplateMatchingSucceededWhileOcrWasPrimary_FlagsOcrAsStale()
    {
        var target = new VisualTarget(OcrText: "Export", TemplateImage: [1, 2, 3]);
        var resolution = new TargetResolution(TargetStrategy.TemplateMatching, new ScreenPoint(10, 20), 0.9);

        var proposal = WorkflowHealer.Propose(target, resolution);

        Assert.NotNull(proposal);
        Assert.Equal(TargetStrategy.Ocr, proposal!.FailedStrategy);
        Assert.Equal(TargetStrategy.TemplateMatching, proposal.SucceededStrategy);
    }
}
