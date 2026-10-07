using ForgeFlow.Domain.Common;

namespace ForgeFlow.Domain.Revisions;

public static class RevisionSequence
{
    // ASME Y14.35: I, O, Q, S, X and Z are never used as revision letters.
    private const string Letters = "ABCDEFGHJKLMNPRTUVWY";

    public const string Initial = "A";

    public static bool IsValid(string? code) =>
        !string.IsNullOrEmpty(code) && code.All(c => Letters.Contains(c));

    public static string Next(string current)
    {
        if (!IsValid(current))
        {
            throw new DomainException($"'{current}' is not a valid revision code.");
        }

        var chars = current.ToCharArray();
        for (var i = chars.Length - 1; i >= 0; i--)
        {
            var index = Letters.IndexOf(chars[i]);
            if (index < Letters.Length - 1)
            {
                chars[i] = Letters[index + 1];
                return new string(chars);
            }

            chars[i] = Letters[0];
        }

        return Letters[0] + new string(chars);
    }
}
