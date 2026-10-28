using NpcFollowing.Recruitment;
using NpcFollowing.StateMachine.StateFeatures;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace NpcFollowing.StateMachine.State;

internal sealed class AvailableState : CompanionState, IActionPerformer
{
    public bool CanPerformAction => !StateMachine.IsDisposed && StateMachine.CompanionManager.CanRecruit();

    public AvailableState(CompanionStateMachine stateMachine, IModEvents events, IMonitor monitor)
        : base(stateMachine, events, monitor) { }

    public bool PerformAction(Farmer who, GameLocation location)
    {
        NPC npc = StateMachine.Companion;
        npc.Halt();
        npc.facePlayer(who);
        location.createQuestionDialogue(
            StateMachine.CompanionManager.Translate("recruit.question", new { npc = npc.displayName }),
            location.createYesNoResponses(),
            (_, answer) =>
            {
                // A delayed answer must never recruit a disposed or already changed session.
                if (answer != "Yes" || !CanPerformAction || StateMachine.CurrentStateFlag != CompanionStateMachine.StateFlag.AVAILABLE)
                    return;
                var reason = RecruitmentRules.GetRejectionReason(StateMachine.Definition,
                    who.getFriendshipHeartLevelForNPC(npc.Name), Game1.timeOfDay);
                string text = StateMachine.CompanionManager.Recruitment.GetDialogue(npc.Name, StateMachine.Definition, reason);
                npc.Halt();
                npc.facePlayer(who);
                if (reason == RecruitmentRejectionReason.None)
                    StateMachine.Recruit();
                else
                    StateMachine.MakeUnavailable();
                // Recruiting clears the dialogue stack; show the result after the transition.
                Game1.DrawDialogue(new Dialogue(npc, null, text));
            }, npc);
        return true;
    }
}
