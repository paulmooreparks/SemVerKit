using System.Collections.Immutable;
using OvertList = Overt.Runtime.List<string>;

namespace ParksComputing.SemVer;

/// <summary>
/// Internal helpers the Overt module binds to via `extern "csharp"`.
/// Each method has a single responsibility and no exception surface:
/// callers in SemVer.ov always validate inputs (via IsDigits, Length,
/// etc.) before calling the parse/index helpers, so neither Int32.Parse
/// nor an out-of-range index can actually fire at runtime.
///
/// When the Overt stdlib grows equivalents for these (str-split, str-
/// is-digits, list-at, etc.), these declarations collapse and the
/// extern block in SemVer.ov goes with them.
/// </summary>
internal static class Strings {
    public static bool StartsWith(string s, string prefix) => s.StartsWith(prefix, StringComparison.Ordinal);

    public static bool IsDigits(string s) {
        if (s.Length == 0) {
            return false;
        }
        foreach (var c in s) {
            if (c < '0' || c > '9') {
                return false;
            }
        }
        return true;
    }

    public static int ToInt(string s) => int.Parse(s, System.Globalization.CultureInfo.InvariantCulture);

    public static OvertList Split(string s, string sep) {
        var parts = s.Split(sep, StringSplitOptions.None);
        return new OvertList(ImmutableArray.Create(parts));
    }

    public static string IntToStr(int n) => n.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public static string ListAt(OvertList list, int index) => list.Items[index];
}
