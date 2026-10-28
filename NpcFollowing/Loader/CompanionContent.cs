using NpcFollowing.Model;
using StardewModdingAPI;
using StardewModdingAPI.Events;

namespace NpcFollowing.Loader;

/// <summary>Owns the public asset; only the manager creates game-session NPC state.</summary>
internal sealed class CompanionContent
{
    public const string AssetName = "Mods/com.szzrain.npc_following/Companions";
    private readonly IModHelper helper;
    private readonly IMonitor monitor;
    public bool IsDirty { get; private set; } = true;

    public CompanionContent(IModHelper helper, IMonitor monitor)
    {
        this.helper = helper;
        this.monitor = monitor;
        helper.Events.Content.AssetRequested += OnAssetRequested;
        helper.Events.Content.AssetsInvalidated += OnAssetsInvalidated;
        helper.Events.Content.LocaleChanged += (_, _) => MarkDirty();
    }

    private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        if (e.NameWithoutLocale.IsEquivalentTo(AssetName))
            e.LoadFrom(() => new Dictionary<string, CompanionDefinition>(), AssetLoadPriority.Exclusive);
    }

    private void OnAssetsInvalidated(object? sender, AssetsInvalidatedEventArgs e)
    {
        if (e.NamesWithoutLocale.Any(name => name.IsEquivalentTo(AssetName)))
            IsDirty = true;
    }

    public void MarkDirty() => IsDirty = true;

    public Dictionary<string, CompanionDefinition>? Load()
    {
        IsDirty = false;
        try
        {
            var asset = helper.GameContent.Load<Dictionary<string, CompanionDefinition>>(AssetName);
            var result = new Dictionary<string, CompanionDefinition>(StringComparer.Ordinal);
            foreach (var entry in asset)
            {
                if (string.IsNullOrWhiteSpace(entry.Key) || entry.Value is null)
                {
                    monitor.Log("Ignoring a companion entry with an empty NPC name or null definition.", LogLevel.Warn);
                    continue;
                }
                if (!entry.Value.Enabled)
                    continue;
                if (!entry.Value.TryValidate(out string? error))
                {
                    monitor.Log($"Ignoring companion '{entry.Key}': {error}", LogLevel.Warn);
                    continue;
                }
                result[entry.Key] = entry.Value.Snapshot();
            }
            return result;
        }
        catch (Exception ex)
        {
            monitor.Log($"Could not load companion content. Keeping the previous candidate list.\n{ex}", LogLevel.Error);
            return null;
        }
    }
}
