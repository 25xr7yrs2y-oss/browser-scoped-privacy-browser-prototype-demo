using System.Text.Json;

namespace PrivacyBrowser.App;

/// <summary>Persists only non-secret UI state. Identity passphrases are never stored.</summary>
public sealed class UserStateStore
{
    private readonly string _path;

    public UserStateStore(string bundleRoot)
    {
        _path = Path.Combine(bundleRoot, "state", "controller-settings.json");
    }

    public string? LoadSelectedIdentityId()
    {
        try
        {
            if (!File.Exists(_path)) return null;
            var state = JsonSerializer.Deserialize<ControllerState>(File.ReadAllText(_path));
            return string.IsNullOrWhiteSpace(state?.SelectedIdentityId) ? null : state.SelectedIdentityId;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public bool SaveSelectedIdentityId(string? identityId)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var temporaryPath = _path + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(new ControllerState(identityId)));
            File.Move(temporaryPath, _path, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private sealed record ControllerState(string? SelectedIdentityId);
}
