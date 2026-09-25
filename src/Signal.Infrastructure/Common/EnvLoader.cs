namespace Signal.Infrastructure.Common;

public static class EnvLoader
{
    public static void Load(string? filePath = null)
    {
        filePath ??= Path.Combine(Directory.GetCurrentDirectory(), ".env");

        // Search parent directories if not found in current directory
        var current = Directory.GetCurrentDirectory();
        while (!File.Exists(filePath) && Directory.GetParent(current) != null)
        {
            var parent = Directory.GetParent(current)!.FullName;
            filePath = Path.Combine(parent, ".env");
            if (File.Exists(filePath))
                break;
            current = parent;
        }

        if (!File.Exists(filePath))
            return;

        foreach (var line in File.ReadAllLines(filePath))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#'))
                continue;

            var parts = trimmed.Split('=', 2);
            if (parts.Length == 2)
            {
                var key = parts[0].Trim();
                var value = parts[1].Trim().Trim('"', '\'');
                if (!string.IsNullOrEmpty(key) && string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
                {
                    Environment.SetEnvironmentVariable(key, value);
                }
            }
        }
    }
}
