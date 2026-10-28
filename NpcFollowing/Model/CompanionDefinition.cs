namespace NpcFollowing.Model;

/// <summary>A content-pack entry in Mods/com.szzrain.npc_following/Companions.</summary>
public sealed class CompanionDefinition
{
    public bool Enabled { get; set; } = true;
    public int MinimumHearts { get; set; } = 10;
    public int LatestRecruitTime { get; set; } = 2200;
    public string? AcceptDialogue { get; set; }
    public string? RejectDialogue { get; set; }
    public string? RejectLateDialogue { get; set; }
    public string? DefaultWeaponId { get; set; }

    public bool TryValidate(out string? error)
    {
        if (MinimumHearts < 0 || MinimumHearts > 14)
        {
            error = "MinimumHearts must be between 0 and 14.";
            return false;
        }

        int minutes = LatestRecruitTime % 100;
        if (LatestRecruitTime < 600 || LatestRecruitTime > 2600 || minutes > 50 || minutes % 10 != 0)
        {
            error = "LatestRecruitTime must be a ten-minute game time between 600 and 2600.";
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>Keep a recruited session independent of content invalidation and CP edits.</summary>
    public CompanionDefinition Snapshot() => (CompanionDefinition)MemberwiseClone();
}
