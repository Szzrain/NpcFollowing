using NpcFollowing.Model;

namespace NpcFollowing.Recruitment;

internal enum RecruitmentRejectionReason
{
    None,
    InsufficientHearts,
    TooLate
}

internal static class RecruitmentRules
{
    public static RecruitmentRejectionReason GetRejectionReason(CompanionDefinition definition, int hearts, int time)
    {
        if (time >= definition.LatestRecruitTime)
            return RecruitmentRejectionReason.TooLate;
        return hearts < definition.MinimumHearts
            ? RecruitmentRejectionReason.InsufficientHearts
            : RecruitmentRejectionReason.None;
    }
}
