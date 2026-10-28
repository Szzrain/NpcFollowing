using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;

namespace NpcFollowing.AI;

internal partial class AI_StateMachine
{
    private bool swimsuit;
    private bool alreadyInAction;
    private string? normalSpriteAsset;
    private readonly HashSet<string> reportedSpriteFailures = new(StringComparer.OrdinalIgnoreCase);

    public void CheckSwimming()
    {
        Vector2 position = this.npc.getStandingPosition();
        GameLocation location = this.npc.currentLocation;
        string action = location.doesTileHaveProperty((int)position.X / Game1.tileSize,
            (int)position.Y / Game1.tileSize, "TouchAction", "Back");
        switch (action)
        {
            case "ChangeIntoSwimsuit":
                this.ChangeIntoSwimsuit();
                break;
            case "ChangeOutOfSwimsuit":
                this.ChangeOutOfSwimsuit();
                break;
            case "PoolEntrance":
                if (this.alreadyInAction)
                    return;
                this.alreadyInAction = true;
                this.SwitchSwimming(location);
                break;
            case "PoolExit":
                this.TriggerSwitchToWalking(location);
                break;
            default:
                this.alreadyInAction = false;
                break;
        }
    }

    public void SwitchSwimming(GameLocation location)
    {
        if (this.npc.swimming.Value)
            this.TriggerSwitchToWalking(location);
        else
            this.TriggerSwitchToSwimming(location);
    }

    public void TriggerSwitchToSwimming(GameLocation location)
    {
        if (this.npc.swimming.Value)
            return;

        this.npc.position.Y += 16f;
        this.npc.swimTimer = 800;
        this.npc.swimming.Value = true;
        location.playSound("pullItemFromWater");
    }

    public void TriggerSwitchToWalking(GameLocation location)
    {
        if (!this.npc.swimming.Value)
            return;

        this.npc.jump();
        this.npc.swimTimer = 800;
        this.npc.position.Y -= 16f;
        this.npc.swimming.Value = false;
        location.playSound("pullItemFromWater");
    }

    public void AfterWarpChangeSwimsuit()
    {
        if (this.swimsuit)
            this.TryApplySwimsuit();
        else
            this.RestoreNormalAppearance();
    }

    public void ChangeIntoSwimsuit()
    {
        if (this.swimsuit)
            return;

        // A forced NPC can use an arbitrary sprite asset with no Data/Characters entry.
        this.normalSpriteAsset = this.npc.Sprite.overrideTextureName ?? this.npc.Sprite.textureName.Value;
        this.swimsuit = true;
        this.TryApplySwimsuit();
    }

    public void ChangeOutOfSwimsuit()
    {
        if (!this.swimsuit)
            return;

        this.swimsuit = false;
        this.RestoreNormalAppearance();
    }

    private void TryApplySwimsuit()
    {
        // getTextureName resolves aliases such as "Mister Qi" -> "MrQi" and is independent
        // of the sprite's current beach texture. Keep already-suffixed aliases valid too.
        string textureName = this.npc.getTextureName();
        string asset = "Characters/" + (textureName.EndsWith("_Beach", StringComparison.OrdinalIgnoreCase)
            ? textureName
            : textureName + "_Beach");
        this.TrySetSpriteTexture(asset);
    }

    private void RestoreNormalAppearance()
    {
        if (this.npc.SimpleNonVillagerNPC || this.npc.GetData() == null)
        {
            if (!string.IsNullOrWhiteSpace(this.normalSpriteAsset))
                this.TrySetSpriteTexture(this.normalSpriteAsset);
            return;
        }

        AnimatedSprite sprite = this.npc.Sprite;
        string previousName = sprite.textureName.Value;
        string previousOverride = sprite.overrideTextureName;
        try
        {
            // Preserve native seasonal, location-specific, and content-pack appearances.
            this.npc.ChooseAppearance();
            _ = this.npc.Sprite.Texture;
            this.normalSpriteAsset = this.npc.Sprite.overrideTextureName ?? this.npc.Sprite.textureName.Value;
        }
        catch (Exception ex)
        {
            sprite.textureName.Value = previousName;
            sprite.overrideTextureName = previousOverride;
            this.ReportSpriteFailure("normal appearance", ex.Message);
        }
    }

    private void TrySetSpriteTexture(string asset)
    {
        AnimatedSprite sprite = this.npc.Sprite;
        string previousName = sprite.textureName.Value;
        string previousOverride = sprite.overrideTextureName;
        try
        {
            if (!Game1.content.DoesAssetExist<Texture2D>(asset))
            {
                this.ReportSpriteFailure(asset, "the asset does not exist");
                return;
            }

            // Validate before changing the live sprite, so a bad asset keeps its existing appearance.
            _ = Game1.content.Load<Texture2D>(asset);
            sprite.LoadTexture(asset);
        }
        catch (Exception ex)
        {
            sprite.textureName.Value = previousName;
            sprite.overrideTextureName = previousOverride;
            this.ReportSpriteFailure(asset, ex.Message);
        }
    }

    private void ReportSpriteFailure(string asset, string reason)
    {
        if (this.reportedSpriteFailures.Add(asset))
            this.Monitor.Log($"Kept {this.npc.Name}'s existing sprite because '{asset}' could not be loaded: {reason}", LogLevel.Debug);
    }
}
