using System.Text.Json;

namespace Tabs.Bot;

public sealed class MatchStore
{
    private readonly Dictionary<ulong, TabsMatch> _active = new();
    private readonly string _root;
    private readonly string _activePath;
    private readonly string _saveRoot;

    public MatchStore()
    {
        _root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TABS Arena Bot");
        _saveRoot = Path.Combine(_root, "saves");
        _activePath = Path.Combine(_root, "active-matches.json");
        Directory.CreateDirectory(_saveRoot);
    }

    public IReadOnlyCollection<TabsMatch> ActiveMatches => _active.Values.ToList();

    public async Task LoadActiveAsync()
    {
        if (!File.Exists(_activePath))
            return;

        try
        {
            var matches = JsonSerializer.Deserialize<List<TabsMatch>>(await File.ReadAllTextAsync(_activePath), TabsJson.Options);
            if (matches == null)
                return;

            _active.Clear();
            foreach (var match in matches)
                _active[match.ChannelId] = match;
        }
        catch
        {
            // If active state is corrupted, named saves still remain intact.
        }
    }

    public TabsMatch? GetActive(ulong channelId)
    {
        return _active.TryGetValue(channelId, out var match) ? match : null;
    }

    public async Task SetActiveAsync(TabsMatch match)
    {
        _active[match.ChannelId] = match;
        await SaveActiveAsync();
    }

    public async Task ClearActiveAsync(ulong channelId)
    {
        if (_active.Remove(channelId))
            await SaveActiveAsync();
    }

    public async Task SaveActiveAsync()
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(_activePath, JsonSerializer.Serialize(_active.Values.ToList(), TabsJson.Options));
    }

    public async Task SaveNamedAsync(TabsMatch match, string name)
    {
        string path = SavePath(GetSaveOwner(match), name);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(match, TabsJson.Options));
    }

    public async Task<TabsMatch?> LoadNamedAsync(ulong ownerUserId, ulong channelId, string name)
    {
        string path = SavePath(ownerUserId, name);
        if (!File.Exists(path))
            return null;

        var match = JsonSerializer.Deserialize<TabsMatch>(await File.ReadAllTextAsync(path), TabsJson.Options);
        if (match == null)
            return null;

        match.ChannelId = channelId;
        return match;
    }

    public IReadOnlyList<string> ListSaves(ulong ownerUserId)
    {
        string prefix = $"{ownerUserId}_";
        return Directory.GetFiles(_saveRoot, $"{prefix}*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => name != null && name.StartsWith(prefix, StringComparison.Ordinal))
            .Select(name => name![prefix.Length..])
            .OrderBy(name => name)
            .ToList();
    }

    public bool DeleteSave(ulong ownerUserId, string name)
    {
        string path = SavePath(ownerUserId, name);
        if (!File.Exists(path))
            return false;

        File.Delete(path);
        return true;
    }

    private static ulong GetSaveOwner(TabsMatch match)
    {
        return match.HostUserId != 0 ? match.HostUserId : match.ChannelId;
    }

    private string SavePath(ulong ownerUserId, string name)
    {
        return Path.Combine(_saveRoot, $"{ownerUserId}_{NormalizeSaveName(name)}.json");
    }

    public static string NormalizeSaveName(string name)
    {
        string safe = string.Concat(name.Trim().Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_'));
        return safe.Length == 0 ? "match" : safe;
    }
}
