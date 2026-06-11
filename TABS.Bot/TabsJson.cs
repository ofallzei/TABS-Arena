using System.Text.Json;

namespace Tabs.Bot;

public static class TabsJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };
}
