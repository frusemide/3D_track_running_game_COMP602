using System.Collections.Generic;

// Stores the finishing order of the race (1st place first) for the podium scene.
public static class RaceResults
{
    public static List<string> FinishOrder = new List<string>();

    static readonly string[] DefaultNames = { "Player 1", "Player 2", "Player 3" };

    public static List<string> GetTopThree()
    {
        List<string> top = new List<string>();
        for (int i = 0; i < 3; i++)
        {
            if (FinishOrder.Count == 0) top.Add(DefaultNames[i]);
            else if (i < FinishOrder.Count) top.Add(FinishOrder[i]);
            else top.Add("-");
        }
        return top;
    }
}
