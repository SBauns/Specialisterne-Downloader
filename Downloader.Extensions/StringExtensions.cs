namespace Downloader.Extensions
{
    public static class StringExtensions
    {
        public static string ToTitleFromScreamingSnakeCase(this string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string[] parts = value.Split('_', StringSplitOptions.RemoveEmptyEntries);

            var result = parts.Select(p =>
            {
                string lower = p.ToLowerInvariant();
                return char.ToUpperInvariant(lower[0]) + lower[1..];
            });

            return string.Join(" ", result);
        }
    }
}