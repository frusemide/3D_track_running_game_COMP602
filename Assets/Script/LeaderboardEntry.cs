using System;

[Serializable]
public class LeaderboardEntry
{
    public string playerName;
    public float time;

    public LeaderboardEntry(string playerName, float time)
    {
        this.playerName = playerName;
        this.time = time;
    }
}
