using System;
using UnityEngine;

namespace BatteryTwin.Data
{
    /// <summary>
    /// Skeleton for the eventual Firebase-backed source. Implements the same
    /// interface as MockBatteryDataSource so swapping is a single inspector
    /// drag — no code changes elsewhere in the twin.
    ///
    /// TODO: wire UnityWebRequest to the Firebase Realtime DB REST endpoint
    /// (https://&lt;project&gt;.firebaseio.com/&lt;path&gt;.json) on a 1 Hz coroutine,
    /// parse the JSON via Newtonsoft into BatteryReading, raise OnReading.
    /// Auth: start with rules allowing anon read; tighten later.
    /// </summary>
    public class FirebaseBatteryDataSource : MonoBehaviour, IBatteryDataSource
    {
        [Header("Firebase")]
        public string projectId = "your-project-id";
        public string databaseUrl = "https://your-project-id-default-rtdb.firebaseio.com";
        public string dataPath = "battery/cell-3";
        public float pollIntervalSec = 1.0f;

        public event Action<BatteryReading> OnReading;
        public bool IsRunning { get; private set; }

        public void StartSource()
        {
            if (IsRunning) return;
            IsRunning = true;
            Debug.LogWarning(
                "FirebaseBatteryDataSource is a stub. Replace this body with a UnityWebRequest poll loop.",
                this);
            // TODO: StartCoroutine(PollLoop());
        }

        public void StopSource()
        {
            if (!IsRunning) return;
            IsRunning = false;
            // TODO: stop coroutine
        }

        // Reference the event so the compiler doesn't warn about it being unused
        // while the stub is in place.
        void OnDestroy()
        {
            if (OnReading != null) { /* no-op */ }
        }
    }
}
