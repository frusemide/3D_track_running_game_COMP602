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

        if (_raceManager.Phase != RaceManager.RacePhase.Results)
            return;

        int raceTick = _raceManager.RaceStartTick;
        if (raceTick == _lastRecordedRaceTick)
            return;

        if (TryRecordLocalResult())
            _lastRecordedRaceTick = raceTick;
    }

    private bool TryRecordLocalResult()
    {
        var runner = _raceManager.Runner;
        if (!runner.TryGetPlayerObject(runner.LocalPlayer, out var obj) || !obj.TryGetComponent<Player>(out var local))
            return false;

        // Spectators who were not in the race have nothing to record.
        if (!local.IsRacing || !local.HasFinished)
            return true;

        int placement = RaceLogic.RankParticipants(runner).IndexOf(local);
        bool won = placement == 0;
        float finishTime = (local.FinishTick - _raceManager.RaceStartTick) * runner.DeltaTime;

        RacerRecords.RecordRace(won, finishTime);
        return true;
    }
}
