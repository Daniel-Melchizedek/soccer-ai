using Vog.Contracts;
using Vog.Domain;

namespace Vog.Simulator;

public sealed record MatchSetup(string MatchId, Club Home, Club Away, ulong Seed, string Competition = "Synthetic Premier Division");

internal sealed class SimPlayer(PlayerProfile profile, SimTeam team, int slot)
{
    public PlayerProfile Profile { get; set; } = profile;

    public SimTeam Team { get; } = team;

    public int Slot { get; } = slot;

    public PitchPoint Pos { get; set; }

    public double Vx { get; set; }

    public double Vy { get; set; }

    public bool OnPitch { get; set; } = true;

    public int Yellows { get; set; }

    public double DistanceRun { get; set; }

    public PitchPoint Target { get; set; }

    public bool Sprint { get; set; }

    public string Id => Profile.PlayerId;

    public bool IsGoalkeeper => Profile.Role == Role.Goalkeeper;
}

internal sealed class SimTeam(Club club, bool isHome)
{
    public Club Club { get; } = club;

    public bool IsHome { get; } = isHome;

    public List<SimPlayer> Players { get; } = [];

    public Queue<PlayerProfile> Bench { get; } = new();

    public List<long> SubstitutionTimesMs { get; } = [];

    public int Score { get; set; }

    public SimTeam Opponent { get; set; } = null!;

    public string Id => Club.ClubId;

    public IEnumerable<SimPlayer> OnPitch => Players.Where(p => p.OnPitch);

    public bool AttacksRight(int period) => IsHome == (period == 1);
}

internal enum BallMode
{
    Owned,
    InFlight,
    Dead,
}

internal enum FlightKind
{
    Pass,
    Shot,
    Loose,
    Out,
}

internal enum ShotResult
{
    Goal,
    Saved,
    OffTarget,
    Blocked,
}

internal sealed class SimBall
{
    public BallMode Mode { get; set; } = BallMode.Dead;

    public SimPlayer? Owner { get; set; }

    public PitchPoint Pos { get; set; } = Pitch.Centre;

    public PitchPoint PrevPos { get; set; } = Pitch.Centre;

    public double Z { get; set; }

    public SimTeam? LastTouch { get; set; }

    // Flight
    public PitchPoint From { get; set; }

    public PitchPoint To { get; set; }

    public long T0 { get; set; }

    public long T1 { get; set; }

    public double PeakZ { get; set; }

    public FlightKind Kind { get; set; }

    public SimPlayer? Target { get; set; }

    public SimPlayer? Kicker { get; set; }

    public ShotResult Shot { get; set; }

    public bool LoftedCross { get; set; }

    public double ShotXg { get; set; }

    // Dead ball
    public MatchEventType RestartType { get; set; } = MatchEventType.KickOff;

    public SimTeam? RestartTeam { get; set; }

    public long RestartAtMs { get; set; }

    public PitchPoint RestartSpot { get; set; } = Pitch.Centre;
}
