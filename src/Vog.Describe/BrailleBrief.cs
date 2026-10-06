using Vog.Contracts;
using Vog.Domain;

namespace Vog.Describe;

public static class BrailleBrief
{
    public const int MaxLength = 40;

    public static string? Format(Utterance utterance, TrackingFrame frame, Realizer realizer)
    {
        var kind = utterance.Intent.Kind switch
        {
            IntentKind.Goal => "goal",
            IntentKind.Shot or IntentKind.ShotOutcome => "shot",
            IntentKind.Card => "card",
            IntentKind.Substitution => "sub",
            IntentKind.Foul => "foul",
            IntentKind.Offside => "offside",
            IntentKind.SetPiece => "set_piece",
            IntentKind.PeriodChange => "period",
            IntentKind.ScoreTime => "score",
            IntentKind.BallLocation => "ball",
            IntentKind.PossessionChange => "possession",
            _ => null,
        };
        if (kind is null || !realizer.Bank.Brief.TryGetValue(kind, out var label))
        {
            return null;
        }

        var (minute, added) = MatchClock.Minute(frame.Period, frame.MatchClockMs);
        var time = added > 0 ? $"{minute}+{added}'" : $"{minute}'";
        var parts = new List<string> { time, label };
        var team = utterance.Intent.Slots.GetValueOrDefault("team") as TeamSlot;
        if (team is not null)
        {
            var codeKey = $"team_{team.KitColour}";
            var code = realizer.Bank.Brief.GetValueOrDefault(codeKey) ?? team.Nickname[..Math.Min(3, team.Nickname.Length)].ToUpperInvariant();
            parts.Add(code);
        }

        var score = utterance.Intent.Slots.GetValueOrDefault("score") as ScoreSlot;
        var scoreText = score is null ? null : $"{score.HomeGoals}-{score.AwayGoals}";
        var player = utterance.Intent.Slots.GetValueOrDefault("player") is PlayerSlot slot ? slot.Name : null;
        var zone = utterance.Intent.Slots.GetValueOrDefault("zone") is ZoneSlot zoneSlot ? realizer.Format(zoneSlot) : null;
        var prefix = string.Join(' ', parts);
        var tail = scoreText is null ? string.Empty : $" {scoreText}";
        var optional = new[] { player, zone }.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        var suffix = string.Join(' ', optional);
        var full = string.Join(' ', new[] { prefix, suffix, scoreText }.Where(s => !string.IsNullOrWhiteSpace(s)));
        if (full.Length <= MaxLength)
        {
            return full;
        }

        if (player is not null)
        {
            var room = MaxLength - prefix.Length - tail.Length - 1;
            if (room > 0)
            {
                return $"{prefix} {player[..Math.Min(room, player.Length)]}{tail}";
            }
        }

        return prefix.Length + tail.Length <= MaxLength ? prefix + tail : prefix[..Math.Min(prefix.Length, MaxLength)];
    }
}