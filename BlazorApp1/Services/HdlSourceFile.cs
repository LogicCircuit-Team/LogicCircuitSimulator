using System.Text.RegularExpressions;

namespace BlazorApp1.Services
{
    public sealed class HdlSourceFile
    {
        public string FileName { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;

        public string FindTopModule()
        {
            var matches = Regex.Matches(Source,
                @"(?m)^\s*module\s+(?:automatic\s+|static\s+)?([A-Za-z_][A-Za-z0-9_$]*)",
                RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
            string fileModule = Path.GetFileNameWithoutExtension(FileName);
            foreach (Match match in matches)
                if (string.Equals(match.Groups[1].Value, fileModule, StringComparison.Ordinal))
                    return fileModule;
            return matches.Count > 0 ? matches[0].Groups[1].Value : fileModule;
        }
    }
}
