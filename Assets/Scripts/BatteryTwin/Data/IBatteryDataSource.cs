using System;

namespace BatteryTwin.Data
{
    /// <summary>
    /// A source of battery telemetry. Mock and Firebase implementations sit
    /// behind this interface so the rest of the twin doesn't know or care
    /// where the data is coming from. Swap-point lives in TwinController.
    /// </summary>
    public interface IBatteryDataSource
    {
        event Action<BatteryReading> OnReading;
        bool IsRunning { get; }
        void StartSource();
        void StopSource();
    }
}
