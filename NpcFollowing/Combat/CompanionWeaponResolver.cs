using StardewModdingAPI;
using StardewValley;
using StardewValley.Tools;

namespace NpcFollowing.Combat;

/// <summary>Resolves each attack's weapon; default weapons never enter the companion's bag.</summary>
internal sealed class CompanionWeaponResolver
{
    internal const string RustySwordId = "(W)0";

    private static readonly IReadOnlyDictionary<string, string> PersonalWeapons = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Alex"] = "(W)25",
        ["Sam"] = "(W)30",
        ["Elliott"] = "(W)35",
        ["Maru"] = "(W)36",
        ["Harvey"] = "(W)37",
        ["Penny"] = "(W)38",
        ["Leah"] = "(W)39",
        ["Abigail"] = "(W)40",
        ["Sebastian"] = "(W)41",
        ["Haley"] = "(W)42",
    };

    private readonly IMonitor monitor;
    private readonly Dictionary<string, MeleeWeapon> defaults = new(StringComparer.Ordinal);
    private readonly HashSet<string> reportedInvalidIds = new(StringComparer.Ordinal);
    private bool reportedMissingFallback;

    internal CompanionWeaponResolver(IMonitor monitor)
    {
        this.monitor = monitor;
    }

    internal MeleeWeapon Resolve(string npcName, IEnumerable<Item?> inventory, string? configuredWeaponId)
    {
        return WeaponSelection.Select(
            inventory.OfType<MeleeWeapon>(),
            weapon => !weapon.isScythe(),
            () => this.TryCreateWeapon(configuredWeaponId),
            () => PersonalWeapons.TryGetValue(npcName, out string? itemId) ? this.TryCreateWeapon(itemId) : null,
            this.CreateRustySword);
    }

    private MeleeWeapon? TryCreateWeapon(string? itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return null;

        if (this.defaults.TryGetValue(itemId, out MeleeWeapon? cached))
            return cached;

        try
        {
            if (ItemRegistry.Create(itemId, allowNull: true) is MeleeWeapon weapon && !weapon.isScythe())
            {
                this.defaults.Add(itemId, weapon);
                return weapon;
            }

            this.ReportInvalidWeapon(itemId, "the item is missing or is not a melee weapon");
        }
        catch (Exception ex)
        {
            this.ReportInvalidWeapon(itemId, ex.Message);
        }

        return null;
    }

    private void ReportInvalidWeapon(string itemId, string reason)
    {
        if (this.reportedInvalidIds.Add(itemId))
            this.monitor.Log($"Cannot use companion weapon '{itemId}': {reason}. Trying the next fallback.", LogLevel.Warn);
    }

    private MeleeWeapon CreateRustySword()
    {
        MeleeWeapon? weapon = this.TryCreateWeapon(RustySwordId);
        if (weapon is not null)
            return weapon;

        const string message = "The Rusty Sword '(W)0' is unavailable. Companion combat cannot continue without a valid weapon.";
        if (!this.reportedMissingFallback)
        {
            this.reportedMissingFallback = true;
            this.monitor.Log(message, LogLevel.Error);
        }

        throw new InvalidOperationException(message);
    }
}
