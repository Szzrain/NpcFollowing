using Microsoft.Xna.Framework;
using NpcFollowing.Model;
using NpcFollowing.Objects;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Objects;

namespace NpcFollowing.Driver;

/// <summary>Serializes delivered packages as vanilla chests and restores them after saving.</summary>
internal class StuffDriver
{
    public List<BagDumpInfo> DumpedBags { get; private set; } = new();
    public IMonitor Monitor { get; }
    public IDataHelper DataHelper { get; }

    public StuffDriver(IDataHelper dataHelper, IMonitor monitor)
    {
        this.DataHelper = dataHelper;
        this.Monitor = monitor;
    }

    public void RegisterEvents(IModEvents events)
    {
        events.GameLoop.Saving += this.GameLoop_Saving;
        events.GameLoop.SaveLoaded += this.GameLoop_SaveLoaded;
        events.GameLoop.Saved += this.GameLoop_Saved;
        events.GameLoop.ReturnedToTitle += this.GameLoop_ReturnedToTitle;
    }

    public void PrepareDeliveredBagsToSave()
    {
        if (Context.IsMultiplayer || Utility.getHomeOfFarmer(Game1.player) is not FarmHouse house)
            return;

        this.DumpedBags.Clear();
        // Replacing entries while enumerating the live dictionary invalidates its iterator.
        foreach (var entry in house.objects.Pairs.ToArray())
        {
            if (entry.Value is not Package bag)
                continue;

            Chest chest = new(true) { TileLocation = entry.Key };
            chest.Items.AddRange(bag.Items);
            house.objects[entry.Key] = chest;
            bag.Items.Clear();
            this.DumpedBags.Add(new BagDumpInfo
            {
                source = bag.GivenFrom,
                giftboxIndex = bag.giftboxIndex.Value,
                message = bag.Message,
                posX = (int)entry.Key.X,
                posY = (int)entry.Key.Y
            });
        }

        this.Monitor.Log($"Prepared {this.DumpedBags.Count} delivered bags for saving.", LogLevel.Trace);
    }

    public void ReviveDeliveredBags()
    {
        if (Context.IsMultiplayer || Utility.getHomeOfFarmer(Game1.player) is not FarmHouse house)
            return;

        int restored = 0;
        foreach (BagDumpInfo info in this.DumpedBags)
        {
            Vector2 position = new(info.posX, info.posY);
            if (!house.objects.TryGetValue(position, out StardewValley.Object obj) || obj is not Chest chest)
            {
                this.Monitor.Log($"Couldn't restore delivered bag at {position}: no chest exists there.", LogLevel.Warn);
                continue;
            }

            // Saved and SaveLoaded can both restore the same package; never transfer it twice.
            if (chest is Package)
                continue;

            Package bag = new(chest.Items.ToList(), position, info.giftboxIndex)
            {
                GivenFrom = info.source,
                Message = info.message
            };
            house.objects[position] = bag;
            chest.Items.Clear();
            restored++;
        }

        this.Monitor.Log($"Restored {restored} delivered bags.", LogLevel.Trace);
    }

    private void GameLoop_ReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
    {
        this.DumpedBags.Clear();
    }

    private void GameLoop_Saved(object? sender, SavedEventArgs e)
    {
        if (!Context.IsMultiplayer)
            this.ReviveDeliveredBags();
    }

    private void GameLoop_SaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        this.DumpedBags.Clear();
        if (Context.IsMultiplayer)
            return;

        try
        {
            this.DumpedBags = this.DataHelper.ReadSaveData<List<BagDumpInfo>>("dumped-bags") ?? new();
            this.ReviveDeliveredBags();
        }
        catch (InvalidOperationException ex)
        {
            this.Monitor.Log($"Couldn't load delivered bags: {ex}", LogLevel.Error);
        }
    }

    private void GameLoop_Saving(object? sender, SavingEventArgs e)
    {
        if (Context.IsMultiplayer)
            return;

        try
        {
            this.PrepareDeliveredBagsToSave();
            this.DataHelper.WriteSaveData("dumped-bags", this.DumpedBags);
        }
        catch (InvalidOperationException ex)
        {
            this.Monitor.Log($"Couldn't save delivered bags: {ex}", LogLevel.Error);
        }
    }
}
