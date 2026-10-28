using NpcFollowing.Model;
using StardewModdingAPI;

namespace NpcFollowing.Recruitment;

internal sealed class RecruitmentService
{
    private readonly IMonitor monitor;
    private readonly Func<string, string> translate;
    private readonly HashSet<string> warnings = new();

    public RecruitmentService(IMonitor monitor, Func<string, string> translate)
    {
        this.monitor = monitor;
        this.translate = translate;
    }

    public string GetDialogue(string name, CompanionDefinition definition, RecruitmentRejectionReason reason)
    {
        string? text;
        string field;
        string fallback;
        switch (reason)
        {
            case RecruitmentRejectionReason.TooLate:
                text = string.IsNullOrWhiteSpace(definition.RejectLateDialogue)
                    ? definition.RejectDialogue : definition.RejectLateDialogue;
                field = nameof(definition.RejectDialogue);
                fallback = "recruit.reject.late.fallback";
                break;
            case RecruitmentRejectionReason.InsufficientHearts:
                text = definition.RejectDialogue;
                field = nameof(definition.RejectDialogue);
                fallback = "recruit.reject.fallback";
                break;
            default:
                text = definition.AcceptDialogue;
                field = nameof(definition.AcceptDialogue);
                fallback = "recruit.accept.fallback";
                break;
        }

        if (!string.IsNullOrWhiteSpace(text))
            return text;

        if (warnings.Add($"{name}/{field}"))
            monitor.Log($"Companion '{name}' has no {field}; using translated fallback dialogue.", LogLevel.Warn);
        return translate(fallback);
    }

    public void Reset() => warnings.Clear();
}
