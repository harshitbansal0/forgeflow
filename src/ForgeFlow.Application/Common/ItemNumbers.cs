namespace ForgeFlow.Application.Common;

public static class ItemNumbers
{
    public const string Pattern = @"^[A-Za-z0-9][A-Za-z0-9\-]{2,39}$";
    public const string ErrorMessage = "Use 3-40 letters, digits or hyphens, starting with a letter or digit.";

    public static string Normalize(string number) => number.Trim().ToUpperInvariant();
}
