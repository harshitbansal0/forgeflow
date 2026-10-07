using System.Text.RegularExpressions;

namespace ForgeFlow.Application.Common;

/// <summary>ECO numbers are derived from the change id (ECO-00042), so searches parse them back to ids.</summary>
public static partial class ChangeNumbers
{
    [GeneratedRegex(@"^ECO-?0*(\d{1,9})$", RegexOptions.IgnoreCase)]
    private static partial Regex Pattern();

    public static bool TryParse(string? text, out int id)
    {
        id = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var match = Pattern().Match(text.Trim());
        return match.Success && int.TryParse(match.Groups[1].Value, out id) && id > 0;
    }
}
