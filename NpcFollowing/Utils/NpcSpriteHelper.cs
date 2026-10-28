using StardewValley;

namespace NpcFollowing.Utils;

/// <summary>Keeps stationary NPC sprites valid while retaining their logical movement direction.</summary>
internal static class NpcSpriteHelper
{
    internal static bool HasStandardFrames(int textureWidth, int textureHeight, int frameWidth, int frameHeight)
    {
        return textureWidth > 0 && textureHeight > 0 && frameWidth > 0 && frameHeight > 0
            && (long)(textureWidth / frameWidth) * (textureHeight / frameHeight) >= 16;
    }

    internal static bool HasStandardFrames(AnimatedSprite sprite)
    {
        var texture = sprite.Texture;
        return texture != null
            && HasStandardFrames(texture.Width, texture.Height, sprite.SpriteWidth, sprite.SpriteHeight);
    }

    internal static void KeepValidStaticFrame(AnimatedSprite sprite)
    {
        if (HasStandardFrames(sprite))
            return;

        sprite.CurrentAnimation = null;
        sprite.oldFrame = 0;
        sprite.CurrentFrame = 0;
    }

    internal static void FaceDirection(NPC npc, int direction)
    {
        if (HasStandardFrames(npc.Sprite))
        {
            npc.faceDirection(direction);
            return;
        }

        npc.FacingDirection = direction;
        KeepValidStaticFrame(npc.Sprite);
    }

    internal static void FaceDirectionStandard(NPC npc, int direction)
    {
        if (HasStandardFrames(npc.Sprite))
        {
            npc.Sprite.faceDirectionStandard(direction);
            return;
        }

        npc.FacingDirection = direction;
        KeepValidStaticFrame(npc.Sprite);
    }

    internal static void FacePlayer(NPC npc, Farmer farmer)
    {
        npc.facePlayer(farmer);
        if (!HasStandardFrames(npc.Sprite))
        {
            npc.FacingDirection = (farmer.FacingDirection + 2) % 4;
            KeepValidStaticFrame(npc.Sprite);
        }
    }
}
