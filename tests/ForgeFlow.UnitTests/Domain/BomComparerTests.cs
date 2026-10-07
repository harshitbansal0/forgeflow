using ForgeFlow.Domain.Products;

namespace ForgeFlow.UnitTests.Domain;

public sealed class BomComparerTests
{
    [Fact]
    public void Compare_ReportsAddedRemovedAndChangedLines()
    {
        BomLine[] from = [new(1, 2m), new(2, 4m), new(3, 1m)];
        BomLine[] to = [new(1, 2m), new(2, 6m), new(4, 1m)];

        var differences = BomComparer.Compare(from, to).ToDictionary(d => d.ComponentId);

        Assert.Equal(3, differences.Count);
        Assert.Equal(new BomDifference(2, BomDifferenceKind.QuantityChanged, 4m, 6m), differences[2]);
        Assert.Equal(new BomDifference(3, BomDifferenceKind.Removed, 1m, null), differences[3]);
        Assert.Equal(new BomDifference(4, BomDifferenceKind.Added, null, 1m), differences[4]);
    }

    [Fact]
    public void Compare_IdenticalBoms_HasNoDifferences()
    {
        BomLine[] bom = [new(1, 2m), new(2, 4m)];

        Assert.Empty(BomComparer.Compare(bom, bom));
    }
}
