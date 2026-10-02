using Vog.Contracts;

namespace Vog.Domain;

/// <summary>Simulation attributes for a fictional player. Speeds in m/s; skills in 0..1.</summary>
public sealed record PlayerProfile(
    string PlayerId,
    string FirstName,
    string LastName,
    int Number,
    Role Role,
    double TopSpeed,
    double Passing,
    double Finishing,
    double Tackling,
    double Stamina)
{
    public string Name => $"{FirstName} {LastName}";

    public PlayerInfo ToInfo() => new(PlayerId, Name, LastName, Number, Role);
}

/// <summary>A fictional club. The first 11 squad entries are the starters, in <see cref="Formation"/> order.</summary>
public sealed record Club(string ClubId, string Name, string Nickname, string KitColour, IReadOnlyList<PlayerProfile> Squad)
{
    public IReadOnlyList<PlayerProfile> Starters => Squad.Take(11).ToList();

    public IReadOnlyList<PlayerProfile> Bench => Squad.Skip(11).ToList();

    public TeamInfo ToInfo() =>
        new(ClubId, Name, Nickname, KitColour, Starters.Select(p => p.ToInfo()).ToList(), Bench.Select(p => p.ToInfo()).ToList());
}
