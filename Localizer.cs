using System.Globalization;

namespace HonorPCHelper;

internal static class L
{
    private static readonly bool IsRussian =
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("ru", StringComparison.OrdinalIgnoreCase);

    internal static string T(string russian, string english) => IsRussian ? russian : english;
}
