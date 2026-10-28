namespace NpcFollowing.Combat;

/// <summary>Selects a weapon without changing inventory slots or the selected item.</summary>
internal static class WeaponSelection
{
    internal static T Select<T>(
        IEnumerable<T?> inventory,
        Func<T, bool> canUse,
        Func<T?> createConfiguredWeapon,
        Func<T?> createPersonalWeapon,
        Func<T> createFallbackWeapon) where T : class
    {
        foreach (T? item in inventory)
        {
            if (item is not null && canUse(item))
                return item;
        }

        return createConfiguredWeapon()
            ?? createPersonalWeapon()
            ?? createFallbackWeapon()
            ?? throw new InvalidOperationException("The fallback weapon could not be created.");
    }
}
