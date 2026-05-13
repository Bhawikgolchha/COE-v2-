using System;

namespace BatteryTwin.Data
{
    /// <summary>
    /// One snapshot of telemetry for a single cell. Immutable.
    /// Power is derived (P = V * I) so producers only fill measured fields.
    /// </summary>
    [Serializable]
    public struct BatteryReading
    {
        public string CellId;
        public float Voltage;       // Volts
        public float Current;       // Amps (signed: +charge / -discharge)
        public float Temperature;   // Celsius
        public float SOC;           // 0..100
        public float SOH;           // 0..100
        public int Cycles;
        public DateTime Timestamp;

        public float Power => Voltage * Current;

        public string Status
        {
            get
            {
                if (Current > 0.1f) return "Charging";
                if (Current < -0.1f) return "Discharging";
                return "Idle";
            }
        }
    }
}
