using UnityEngine;

// Updates the local racer's records once per race, when the race reaches the Results phase.
// Created automatically at startup, so it needs no scene setup.
public class RecordsTracker : MonoBehaviour
{
    private const float SearchInterval = 0.5f;

    private RaceManager _raceManager;
    private float _nextSearch;
    private int _lastRecordedRaceTick = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        var go = new GameObject("RecordsTracker");
        DontDestroyOnLoad(go);
        go.AddComponent<RecordsTracker>();
    }

    private void Update()
    {
        if (_raceManager == null)
        {
            if (Time.unscaledTime < _nextSearch)
                return;
            _nextSearch = Time.unscaledTime + SearchInterval;

            _raceManager = FindFirstObjectByType<RaceManager>();
            _lastRecordedRaceTick = -1;
            return;
        }

        if (_raceManager.Object == null || !_raceManager.Object.IsValid || _raceManager.Runner == null)
            return;

        // Podium is included in case this client missed the short Results phase.
        RaceManager.RacePhase phase = _raceManager.Phase;
        if (phase != RaceManager.RacePhase.Results && phase != RaceManager.RacePhase.Podium)
            return;

        int raceTick = _raceManager.RaceStartTick;
        if (raceTick == _lastRecordedRaceTick)
            return;

        if (TryRecordLocalResult())
            _lastRecordedRaceTick = raceTick;
    }

    // Returns false while the result is not ready yet, so it is checked again next frame.
    private bool TryRecordLocalResult()
    {
        var runner = _raceManager.Runner;
        Player local = Player.FindLocal();
        if (local == null)
            return false;

        // Spectators who were not in the race have nothing to record.
        if (!local.IsRacing)
            return true;

        // Updates for different objects can reach a client at different times, so this waits
        // until the host's finishing position for this player has arrived.
        if (!local.HasFinished || local.FinishOrder <= 0)
            return false;

        bool won = local.FinishOrder == 1;
        float finishTime = (local.FinishTick - _raceManager.RaceStartTick) * runner.DeltaTime;

        RacerRecords.RecordRace(won, finishTime);
        return true;
    }
}
