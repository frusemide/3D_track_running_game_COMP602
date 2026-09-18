using System.Collections.Generic;
using Fusion;

// Shared, cross-player race logic used by BOTH the real RaceManager and the lobby
// PracticeManager. Per-player actions (mark racing, record finish) live on Player;
// this static class holds the operations that span the SET of racing players:
// "have all finished?", "rank them", "who won".
//
// All operations read per-player networked race state (Player.IsRacing / HasFinished /
// FinishTick), so they work identically regardless of how players joined the race.
public static class RaceLogic
{
    // Collect every player currently in the race (IsRacing == true).
    public static List<Player> GetParticipants(NetworkRunner runner)
    {
        var participants = new List<Player>();
        foreach (var player in runner.ActivePlayers)
        {
            if (runner.TryGetPlayerObject(player, out var obj) &&
                obj.TryGetComponent<Player>(out var p) &&
                p.IsRacing)
            {
                participants.Add(p);
            }
        }
        return participants;
    }

    // True once every racing player has finished. A player who has left the session
    // is no longer in ActivePlayers, so they're naturally excluded (won't block the end).
    // Returns false if there are no participants (nothing to finish).
    public static bool AllFinished(NetworkRunner runner)
    {
        var participants = GetParticipants(runner);
        if (participants.Count == 0)
            return false;

        foreach (var p in participants)
        {
            if (!p.HasFinished)
                return false;
        }
        return true;
    }

    // Racing players ordered by finish position: finishers first (lowest FinishTick =
    // earliest = best), then any non-finishers after. Used for winner/standings.
    public static List<Player> RankParticipants(NetworkRunner runner)
    {
        var participants = GetParticipants(runner);

        participants.Sort((a, b) =>
        {
            // Finishers rank ahead of non-finishers.
            if (a.HasFinished && !b.HasFinished) return -1;
            if (!a.HasFinished && b.HasFinished) return 1;
            // Both finished: earlier tick wins.
            if (a.HasFinished && b.HasFinished)
                return a.FinishTick.CompareTo(b.FinishTick);
            // Neither finished yet: rank by current race progress (further along wins).
            // Keeps this consistent with the progress-bar leader/crown logic in RaceHud,
            // and makes RankParticipants meaningful mid-race, not just once people finish.
            return b.transform.position.z.CompareTo(a.transform.position.z);
        });

        return participants;
    }

    // The winner (first place), or null if no one has finished / no participants.
    public static Player Winner(NetworkRunner runner)
    {
        var ranked = RankParticipants(runner);
        if (ranked.Count > 0 && ranked[0].HasFinished)
            return ranked[0];
        return null;
    }
}
