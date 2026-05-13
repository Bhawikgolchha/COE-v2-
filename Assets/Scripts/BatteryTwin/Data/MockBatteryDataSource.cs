using System;
using System.Collections;
using UnityEngine;

namespace BatteryTwin.Data
{
    /// <summary>
    /// Procedural mock telemetry. SoC ramps as a triangle wave, voltage
    /// tracks SoC, current sign flips on charge/discharge halves, temperature
    /// random-walks with a current-dependent bias, cycles increment on each
    /// full SoC cycle.
    /// </summary>
    public class MockBatteryDataSource : MonoBehaviour, IBatteryDataSource
    {
        [Header("Cell identity")]
        public string cellId = "Cell 3";

        [Header("Cadence")]
        [Tooltip("Seconds between readings.")]
        public float updateInterval = 1.0f;

        [Header("Ranges")]
        public Vector2 voltRange = new Vector2(2.5f, 4.2f);
        public Vector2 tempRange = new Vector2(20f, 55f);
        public Vector2 currentRange = new Vector2(-30f, 30f);

        [Header("Behaviour")]
        [Tooltip("Seconds for a full SoC 0->100->0 cycle.")]
        public float socPeriodSec = 60f;
        public int cycleCountStart = 42;
        public float startingSOH = 78f;
        [Tooltip("Per-second SOH degradation (small). Pure cosmetics.")]
        public float sohDecayPerSec = 0.0001f;

        public event Action<BatteryReading> OnReading;
        public bool IsRunning { get; private set; }

        float _temperature;
        int _cycles;
        float _soh;
        Coroutine _loop;

        void Awake()
        {
            _temperature = (tempRange.x + tempRange.y) * 0.5f;
            _cycles = cycleCountStart;
            _soh = startingSOH;
        }

        void OnEnable() => StartSource();
        void OnDisable() => StopSource();

        public void StartSource()
        {
            if (IsRunning) return;
            IsRunning = true;
            _loop = StartCoroutine(EmitLoop());
        }

        public void StopSource()
        {
            if (!IsRunning) return;
            IsRunning = false;
            if (_loop != null) StopCoroutine(_loop);
            _loop = null;
        }

        IEnumerator EmitLoop()
        {
            var wait = new WaitForSeconds(Mathf.Max(0.01f, updateInterval));
            int lastHalf = -1; // 0 = charging half, 1 = discharging half
            while (IsRunning)
            {
                float t = Time.time;
                float phase = (t % socPeriodSec) / socPeriodSec; // 0..1
                float soc = (phase < 0.5f)
                    ? Mathf.Lerp(0f, 100f, phase / 0.5f)
                    : Mathf.Lerp(100f, 0f, (phase - 0.5f) / 0.5f);
                int half = (phase < 0.5f) ? 0 : 1;

                // Voltage tracks SoC across configured range with small noise.
                float voltage = Mathf.Lerp(voltRange.x, voltRange.y, soc / 100f)
                                + UnityEngine.Random.Range(-0.02f, 0.02f);

                // Current: charging half = positive, discharging half = negative.
                // Magnitude scales with how fast SoC is moving (|dSoC/dt| is constant here,
                // so we just pick a value in the configured range and apply sign).
                float maxA = Mathf.Max(Mathf.Abs(currentRange.x), Mathf.Abs(currentRange.y));
                float baseMag = maxA * 0.5f + UnityEngine.Random.Range(-2f, 2f);
                float current = (half == 0 ? +1f : -1f) * Mathf.Clamp(baseMag, 0f, maxA);
                current = Mathf.Clamp(current, currentRange.x, currentRange.y);

                // Temperature random-walks, biased upward when |current| is high.
                float bias = (Mathf.Abs(current) / Mathf.Max(1f, maxA)) * 0.3f;
                _temperature += UnityEngine.Random.Range(-0.5f, 0.5f) + bias;
                _temperature = Mathf.Clamp(_temperature, tempRange.x, tempRange.y);

                // Increment cycles on the charging->discharging flip.
                if (lastHalf == 0 && half == 1) _cycles += 1;
                lastHalf = half;

                // Slow SOH drift.
                _soh = Mathf.Max(0f, _soh - sohDecayPerSec * updateInterval);

                var reading = new BatteryReading
                {
                    CellId = cellId,
                    Voltage = voltage,
                    Current = current,
                    Temperature = _temperature,
                    SOC = soc,
                    SOH = _soh,
                    Cycles = _cycles,
                    Timestamp = DateTime.Now,
                };

                try { OnReading?.Invoke(reading); }
                catch (Exception e) { Debug.LogException(e, this); }

                yield return wait;
            }
        }
    }
}
