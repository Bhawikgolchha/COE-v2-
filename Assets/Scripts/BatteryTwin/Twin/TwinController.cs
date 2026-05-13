using System.Collections.Generic;
using BatteryTwin.Bindings;
using BatteryTwin.Data;
using UnityEngine;

namespace BatteryTwin.Twin
{
    /// <summary>
    /// One reading goes in, all bindings update. The active data source is a
    /// MonoBehaviour reference so it can be swapped from the inspector — drag
    /// in MockBatteryDataSource for v1, FirebaseBatteryDataSource later.
    /// </summary>
    public class TwinController : MonoBehaviour
    {
        [Header("Source (must implement IBatteryDataSource)")]
        public MonoBehaviour activeSourceBehaviour;

        [Header("Bindings")]
        public List<TemperatureColorBinding> temperatureBindings = new();
        public List<SocEmissionBinding> socEmissionBindings = new();
        public List<CurrentParticleBinding> currentBindings = new();
        public List<DashboardTextBinding> textBindings = new();
        public List<GaugeBinding> gaugeBindings = new();
        public List<RollingLineChart> charts = new();
        public List<IndicatorCellBinding> indicatorBindings = new();

        public BatteryReading LastReading { get; private set; }
        public float LastReadingTime { get; private set; } = -999f;
        public bool HasReading { get; private set; }

        IBatteryDataSource _source;

        void OnEnable()
        {
            _source = activeSourceBehaviour as IBatteryDataSource;
            if (_source == null)
            {
                Debug.LogError(
                    $"TwinController: activeSourceBehaviour ({activeSourceBehaviour}) does not implement IBatteryDataSource.",
                    this);
                return;
            }
            _source.OnReading += HandleReading;
            _source.StartSource();
        }

        void OnDisable()
        {
            if (_source != null)
            {
                _source.OnReading -= HandleReading;
                _source.StopSource();
            }
        }

        void HandleReading(BatteryReading reading)
        {
            LastReading = reading;
            LastReadingTime = Time.time;
            HasReading = true;

            foreach (var b in temperatureBindings) if (b) b.Apply(reading);
            foreach (var b in socEmissionBindings) if (b) b.Apply(reading);
            foreach (var b in currentBindings)     if (b) b.Apply(reading);
            foreach (var b in textBindings)        if (b) b.Apply(reading);
            foreach (var b in gaugeBindings)       if (b) b.Apply(reading);
            foreach (var c in charts)              if (c) c.PushSample(reading);
            foreach (var b in indicatorBindings)   if (b) b.Apply(reading);
        }

        public string ActiveSourceTypeName =>
            _source != null ? _source.GetType().Name : "(none)";

        public float SecondsSinceLastReading =>
            HasReading ? Time.time - LastReadingTime : float.PositiveInfinity;
    }
}
