namespace ForgeFlow.Domain.Products;

public enum BomDifferenceKind
{
    Added,
    Removed,
    QuantityChanged
}

public sealed record BomLine(int ComponentId, decimal Quantity);

public sealed record BomDifference(int ComponentId, BomDifferenceKind Kind, decimal? FromQuantity, decimal? ToQuantity);

public static class BomComparer
{
    public static IReadOnlyList<BomDifference> Compare(IEnumerable<BomLine> from, IEnumerable<BomLine> to)
    {
        var fromLines = from.ToDictionary(l => l.ComponentId, l => l.Quantity);
        var toLines = to.ToDictionary(l => l.ComponentId, l => l.Quantity);
        var differences = new List<BomDifference>();

        foreach (var (componentId, quantity) in toLines)
        {
            if (!fromLines.TryGetValue(componentId, out var previous))
            {
                differences.Add(new BomDifference(componentId, BomDifferenceKind.Added, null, quantity));
            }
            else if (previous != quantity)
            {
                differences.Add(new BomDifference(componentId, BomDifferenceKind.QuantityChanged, previous, quantity));
            }
        }

        differences.AddRange(fromLines
            .Where(line => !toLines.ContainsKey(line.Key))
            .Select(line => new BomDifference(line.Key, BomDifferenceKind.Removed, line.Value, null)));

        return differences;
    }
}
