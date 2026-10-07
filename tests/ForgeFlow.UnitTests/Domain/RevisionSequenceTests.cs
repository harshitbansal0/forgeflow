using ForgeFlow.Domain.Common;
using ForgeFlow.Domain.Products;
using ForgeFlow.Domain.Revisions;

namespace ForgeFlow.UnitTests.Domain;

public sealed class RevisionSequenceTests
{
    [Theory]
    [InlineData("A", "B")]
    [InlineData("H", "J")]
    [InlineData("N", "P")]
    [InlineData("P", "R")]
    [InlineData("R", "T")]
    [InlineData("W", "Y")]
    [InlineData("Y", "AA")]
    [InlineData("AA", "AB")]
    [InlineData("AY", "BA")]
    [InlineData("YY", "AAA")]
    public void Next_SkipsLettersReservedByAsmeY1435(string current, string expected)
    {
        Assert.Equal(expected, RevisionSequence.Next(current));
    }

    [Theory]
    [InlineData("")]
    [InlineData("I")]
    [InlineData("O")]
    [InlineData("a")]
    [InlineData("A1")]
    public void Next_RejectsInvalidCodes(string current)
    {
        Assert.Throws<DomainException>(() => RevisionSequence.Next(current));
    }

    [Fact]
    public void OrderByRevision_SortsByLengthThenLetters()
    {
        var revisions = new[] { "AA", "B", "Y", "A" }.Select(code => new ProductRevision { RevisionCode = code });

        var ordered = revisions.OrderByRevision().Select(r => r.RevisionCode);

        Assert.Equal(new[] { "A", "B", "Y", "AA" }, ordered);
    }
}
