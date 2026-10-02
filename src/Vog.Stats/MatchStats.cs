namespace Vog.Stats;

public sealed class TeamStats
{
    public int PassesAttempted { get; set; }

    public int PassesCompleted { get; set; }

    public int Shots { get; set; }

    public int ShotsOnTarget { get; set; }

    public double Xg { get; set; }

    public int Goals { get; set; }

    public int Fouls { get; set; }

    public int YellowCards { get; set; }

    public int RedCards { get; set; }

    public int Corners { get; set; }

    public int ThrowIns { get; set; }

    public int GoalKicks { get; set; }

    public int Offsides { get; set; }

    public int Tackles { get; set; }

    public long PossessionMs { get; set; }

    public double PassCompletion => PassesAttempted == 0 ? 0 : (double)PassesCompleted / PassesAttempted;
}

/// <summary>Running totals for the match, per team.</summary>
public sealed class MatchStats(string homeId, string awayId)
{
    public TeamStats Home { get; } = new();

    public TeamStats Away { get; } = new();

    public long InPlayMs { get; set; }

    public TeamStats For(string teamId) => teamId == homeId ? Home : Away;

    public double PossessionShare(string teamId)
    {
        var total = Home.PossessionMs + Away.PossessionMs;
        return total == 0 ? 0.5 : (double)For(teamId).PossessionMs / total;
    }

    public bool IsHome(string teamId) => teamId == homeId;

    public string AwayId => awayId;

    public string HomeId => homeId;
}
