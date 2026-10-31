using HarmonyLib;
using NpcFollowing.Driver;
using NpcFollowing.Loader;
using NpcFollowing.Patches;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace NpcFollowing;

/// <summary>Wires the content provider and the current save's companion lifecycle.</summary>
internal sealed class ModEntry : Mod
{
    public static ModEntry Instance { get; private set; } = null!;
    public static CompanionManager companionManager { get; private set; } = null!;

    public override void Entry(IModHelper helper)
    {
        Instance = this;
        var content = new CompanionContent(helper, Monitor);
        companionManager = new CompanionManager(helper, Monitor, content);
        var stuff = new StuffDriver(helper.Data, Monitor);
        stuff.RegisterEvents(helper.Events);
        CompanionPatches.Initialize(companionManager, helper, Monitor);
        var harmony = new Harmony(ModManifest.UniqueID);
        harmony.Patch(AccessTools.Method(typeof(NPC), nameof(NPC.checkAction)),
            prefix: new HarmonyMethod(typeof(CompanionPatches), nameof(CompanionPatches.CheckActionPrefix)),
            postfix: new HarmonyMethod(typeof(CompanionPatches), nameof(CompanionPatches.CheckActionPostfix)));
        harmony.Patch(AccessTools.Method(typeof(Game1), nameof(Game1.pressUseToolButton), Type.EmptyTypes),
            prefix: new HarmonyMethod(typeof(CompanionPatches), nameof(CompanionPatches.UseToolInteractionPrefix)),
            finalizer: new HarmonyMethod(typeof(CompanionPatches), nameof(CompanionPatches.UseToolInteractionFinalizer)));
        harmony.Patch(AccessTools.Method(typeof(NPC), nameof(NPC.returnHomeFromFarmPosition)),
            prefix: new HarmonyMethod(typeof(CompanionPatches), nameof(CompanionPatches.ReturnHomePrefix)));
        harmony.Patch(AccessTools.Method(typeof(Game1), nameof(Game1.newDayAfterFade), new[] { typeof(Action) }),
            prefix: new HarmonyMethod(typeof(CompanionPatches), nameof(CompanionPatches.NewDayAfterFadePrefix)));

        helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        helper.Events.GameLoop.DayStarted += OnDayStarted;
        helper.Events.GameLoop.DayEnding += OnDayEnding;
        helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        helper.Events.GameLoop.ReturnedToTitle += OnReturnedToTitle;
    }

    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e) => companionManager.InitializeCompanions();
    private void OnDayStarted(object? sender, DayStartedEventArgs e) => companionManager.NewDaySetup();
    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e) => companionManager.Update();
    private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e) => companionManager.UninitializeCompanions();

    private void OnDayEnding(object? sender, DayEndingEventArgs e)
    {
        if (!Context.IsWorldReady || Context.IsMultiplayer)
            return;
        companionManager.ResetStateMachines();
        companionManager.DumpCompanionNonEmptyBags();
    }
}
