using Vog.Contracts;

namespace Vog.Stats;

/// <summary>
/// Speed, distance and sprint detection from 10 Hz positions. Sprints use the common
/// 25.2 km/h threshold, held for at least one second, with hysteresis to avoid flicker.
/// </summary>
public sealed class PlayerMotionTracker
{
    public const double SprintThresholdMs = 7.0;
    public const double SprintExitMs = 6.5;
    public const double HighSpeedThresholdMs = 5.5;
    public const long MinSprintMs = 1000;

    /// <summary>Above this a position jump is a reset (kick-off, restart), not running.</summary>
    private const double TeleportSpeedMs = 12.0;

    private readonly Dictionary<string, PlayerMotion> _players = [];
    private double _matchTopSpeedMs;

    public IReadOnlyDictionary<string, PlayerMotion> Players => _players;

    public void Update(TrackingFrame frame, double dtSeconds, List<PlayerTag> tags)
    {
        foreach (var pf in frame.Players)
        {
            if (!_players.TryGetValue(pf.PlayerId, out var m))
            {
                _players[pf.PlayerId] = new PlayerMotion(pf.PlayerId, pf.TeamId) { Last = pf.Position };
                continue;
            }

            var step = m.Last.DistanceTo(pf.Position);
            m.Last = pf.Position;
            var raw = step / dtSeconds;
            if (raw > TeleportSpeedMs)
            {
                m.SpeedMs = 0;
                EndSprint(m, frame.MatchClockMs, tags);
                continue;
            }

            m.DistanceM += step;
            m.SpeedMs = 0.5 * raw + 0.5 * m.SpeedMs;
            if (m.SpeedMs >= HighSpeedThresholdMs)
            {
                m.HighSpeedDistanceM += step;
            }

            if (m.SpeedMs > m.TopSpeedMs)
            {
                m.TopSpeedMs = m.SpeedMs;
            }

            if (m.SprintStartMs is null && m.SpeedMs >= SprintThresholdMs)
            {
                m.SprintStartMs = frame.MatchClockMs;
                m.SprintPeakMs = m.SpeedMs;
            }
            else if (m.SprintStartMs is not null)
            {
                m.SprintPeakMs = Math.Max(m.SprintPeakMs, m.SpeedMs);
                if (m.SpeedMs < SprintExitMs)
                {
                    EndSprint(m, frame.MatchClockMs, tags);
                }
            }

            var km = (int)(m.DistanceM / 1000);
            if (km >= 5 && km % 5 == 0 && km > m.LastMilestoneKm)
            {
                m.LastMilestoneKm = km;
                tags.Add(new PlayerTag(PlayerTagKind.DistanceMilestone, m.PlayerId, m.TeamId, frame.MatchClockMs, km));
            }
        }
    }

    private void EndSprint(PlayerMotion m, long clockMs, List<PlayerTag> tags)
    {
        if (m.SprintStartMs is not { } start)
        {
            return;
        }

        m.SprintStartMs = null;
        if (clockMs - start < MinSprintMs)
        {
            return;
        }

        m.Sprints++;
        var peakKmh = Math.Round(m.SprintPeakMs * 3.6, 1);
        tags.Add(new PlayerTag(PlayerTagKind.Sprint, m.PlayerId, m.TeamId, clockMs, peakKmh));
        if (m.SprintPeakMs > _matchTopSpeedMs && peakKmh >= 30)
        {
            _matchTopSpeedMs = m.SprintPeakMs;
            tags.Add(new PlayerTag(PlayerTagKind.TopSpeed, m.PlayerId, m.TeamId, clockMs, peakKmh));
        }
    }
}

public sealed class PlayerMotion(string playerId, string teamId)
{
    public string PlayerId { get; } = playerId;

    public string TeamId { get; } = teamId;

    public PitchPoint Last { get; set; }

    public double SpeedMs { get; set; }

    public double TopSpeedMs { get; set; }

    public double DistanceM { get; set; }

    public double HighSpeedDistanceM { get; set; }

    public int Sprints { get; set; }

    internal long? SprintStartMs { get; set; }

    internal double SprintPeakMs { get; set; }

    internal int LastMilestoneKm { get; set; }
}
