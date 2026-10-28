using NpcFollowing.Loader;
using NpcFollowing.Model;
using NpcFollowing.Recruitment;
using NpcFollowing.StateMachine;
using NpcFollowing.StateMachine.State;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Monsters;

namespace NpcFollowing;

/// <summary>Coordinates content definitions and NPC state for the currently loaded single-player save.</summary>
internal sealed class CompanionManager
{
    private readonly IModHelper helper;
    private readonly IMonitor monitor;
    private readonly CompanionContent content;
    private readonly HashSet<string> missingWarnings = new();
    private readonly HashSet<string> unavailableToday = new(StringComparer.Ordinal);
    private Dictionary<string, CompanionDefinition> definitions = new(StringComparer.Ordinal);
    private bool refreshPending;
    private bool initialized;

    public Dictionary<string, CompanionStateMachine> PossibleCompanions { get; } = new(StringComparer.Ordinal);
    public RecruitmentService Recruitment { get; }
    public Farmer Farmer => Game1.player;

    public CompanionManager(IModHelper helper, IMonitor monitor, CompanionContent content)
    {
        this.helper = helper;
        this.monitor = monitor;
        this.content = content;
        Recruitment = new RecruitmentService(monitor, key => Translate(key));
    }

    public string Translate(string key, object? tokens = null) => helper.Translation.Get(key, tokens).ToString();
    internal bool CanRecruit() => initialized && Context.IsWorldReady && !Context.IsMultiplayer;
    internal bool IsUnavailableToday(string name) => unavailableToday.Contains(name);
    internal void MarkUnavailableToday(string name) => unavailableToday.Add(name);
    internal bool IsCandidate(NPC npc) => PossibleCompanions.TryGetValue(npc.Name, out var state)
        && ReferenceEquals(state.Companion, npc)
        && (state.CurrentStateFlag == CompanionStateMachine.StateFlag.RECRUITED || IsRegularCandidate(npc));

    private static bool IsSocialCandidate(NPC npc) => npc is not (Monster or Horse or Junimo or Child or Pet)
        && npc.CanSocialize;

    private bool IsRegularCandidate(NPC npc) => definitions.ContainsKey(npc.Name) && IsSocialCandidate(npc);

    /// <summary>The debug tool grants a temporary session without changing NPC social data or the content whitelist.</summary>
    internal bool ToggleDebugCompanion(NPC npc)
    {
        if (!CanRecruit())
            return false;
        if (string.IsNullOrWhiteSpace(npc.Name) || npc.Sprite is null || npc.currentLocation?.Map is null)
        {
            monitor.Log($"Cannot force recruit '{npc.Name}': the NPC has no usable name, sprite, or map location.", LogLevel.Warn);
            return false;
        }

        if (PossibleCompanions.TryGetValue(npc.Name, out var existing))
        {
            if (ReferenceEquals(existing.Companion, npc))
            {
                if (existing.CurrentStateFlag == CompanionStateMachine.StateFlag.RECRUITED)
                {
                    existing.Dismiss();
                    return true;
                }
            }
            else
            {
                if (existing.CurrentStateFlag == CompanionStateMachine.StateFlag.RECRUITED)
                {
                    monitor.Log($"Cannot force recruit '{npc.Name}': another NPC instance with the same name is already following.", LogLevel.Warn);
                    return false;
                }
                if (!ReleaseCompanion(existing))
                    return false;
                existing = null;
            }
        }

        bool created = existing is null;
        var state = existing ?? CreateCompanion(npc,
            definitions.TryGetValue(npc.Name, out var definition) ? definition : new CompanionDefinition());
        try
        {
            state.MakeAvailable();
            if (state.Recruit(force: true))
            {
                monitor.Log($"Force-recruited debug companion '{npc.Name}'.", LogLevel.Debug);
                return true;
            }
        }
        catch
        {
            if (created)
                ReleaseCompanion(state);
            else
                state.MakeUnavailable();
            throw;
        }

        if (created)
            ReleaseCompanion(state);
        return false;
    }

    private CompanionStateMachine CreateCompanion(NPC npc, CompanionDefinition definition)
    {
        var state = new CompanionStateMachine(this, npc, definition, monitor);
        state.Setup(new Dictionary<CompanionStateMachine.StateFlag, ICompanionState>
        {
            [CompanionStateMachine.StateFlag.RESET] = new ResetState(state, helper.Events, monitor),
            [CompanionStateMachine.StateFlag.AVAILABLE] = new AvailableState(state, helper.Events, monitor),
            [CompanionStateMachine.StateFlag.RECRUITED] = new RecruitedState(state, helper.Events, monitor),
            [CompanionStateMachine.StateFlag.UNAVAILABLE] = new UnavailableState(state, helper.Events, monitor)
        });
        PossibleCompanions.Add(npc.Name, state);
        return state;
    }

    private bool ReleaseCompanion(CompanionStateMachine state)
    {
        if (!state.DumpBagInFarmHouse())
            return false;
        state.Dispose();
        PossibleCompanions.Remove(state.Name);
        return true;
    }

    public void InitializeCompanions()
    {
        UninitializeCompanions();
        if (!Context.IsWorldReady || Context.IsMultiplayer)
            return;
        initialized = true;
        content.MarkDirty();
        RefreshCandidates();
        monitor.Log($"Loaded {PossibleCompanions.Count} companion candidates from content packs.", LogLevel.Info);
    }

    public void Update()
    {
        if (CanRecruit() && Context.IsPlayerFree && Game1.activeClickableMenu is null && (content.IsDirty || refreshPending))
            RefreshCandidates();
    }

    private void RefreshCandidates()
    {
        if (content.IsDirty)
        {
            var loaded = content.Load();
            if (loaded is not null)
                definitions = loaded;
        }
        refreshPending = false;

        foreach (var pair in PossibleCompanions.ToArray())
        {
            var state = pair.Value;
            if (state.CurrentStateFlag == CompanionStateMachine.StateFlag.RECRUITED)
                continue;
            NPC? npc = Game1.getCharacterFromName(pair.Key, false);
            if (npc is null || !IsRegularCandidate(npc) || !ReferenceEquals(npc, state.Companion))
            {
                state.MakeUnavailable();
                ReleaseCompanion(state);
            }
            else
                state.Definition = definitions[pair.Key].Snapshot();
        }

        foreach (var pair in definitions)
        {
            if (PossibleCompanions.ContainsKey(pair.Key))
                continue;
            NPC? npc = Game1.getCharacterFromName(pair.Key, false);
            if (npc is null || !IsSocialCandidate(npc))
            {
                if (missingWarnings.Add(pair.Key))
                    monitor.Log($"Companion '{pair.Key}' is not an available social NPC in this save; skipping it.", LogLevel.Warn);
                continue;
            }
            var state = CreateCompanion(npc, pair.Value);
            state.SetupForNewDay();
        }
    }

    public void NewDaySetup()
    {
        if (!CanRecruit())
            return;
        foreach (var state in PossibleCompanions.Values.Where(state => state.IsDebugSession).ToArray())
            state.Dismiss();
        unavailableToday.Clear();
        content.MarkDirty();
        RefreshCandidates();
        foreach (var state in PossibleCompanions.Values.ToArray())
            state.SetupForNewDay();
    }

    public bool CheckAction(Farmer who, NPC npc, GameLocation location) => CanRecruit()
        && IsCandidate(npc)
        && PossibleCompanions.TryGetValue(npc.Name, out var state)
        && ReferenceEquals(state.Companion, npc) && state.CheckAction(who, location);

    internal void CompanionRecuited(string name) => monitor.Log($"Recruited companion '{name}'.", LogLevel.Debug);
    internal void CompanionDissmised(CompanionStateMachine state)
    {
        // A debug-only NPC must not become an ordinary recruit after its session ends.
        if (!IsRegularCandidate(state.Companion))
            ReleaseCompanion(state);
        refreshPending = true;
    }

    public void ResetStateMachines()
    {
        foreach (var state in PossibleCompanions.Values.ToArray())
        {
            if (state.CurrentStateFlag == CompanionStateMachine.StateFlag.RECRUITED)
                state.Dismiss();
            else
                state.ResetStateMachine();
        }
    }

    public void DumpCompanionNonEmptyBags()
    {
        foreach (var state in PossibleCompanions.Values)
            state.DumpBagInFarmHouse();
    }

    public void UninitializeCompanions()
    {
        foreach (var state in PossibleCompanions.Values)
            state.Dispose();
        PossibleCompanions.Clear();
        definitions.Clear();
        missingWarnings.Clear();
        unavailableToday.Clear();
        Recruitment.Reset();
        initialized = false;
        refreshPending = false;
        content.MarkDirty();
    }
}
