using System;
using BatteryTwin.Data;
using TMPro;
using UnityEngine;

namespace BatteryTwin.Bindings
{
    public enum BatteryField
    {
        Voltage,
        Current,
        Temperature,
        SOC,
        SOH,
        Cycles,
        Power,
        Status,
        CellId,
        Time,
    }

    /// <summary>
    /// One TMP label, one field. Verbose by design — every dashboard label
    /// gets its own component, wired in the inspector. No reflection, no
    /// stringly-typed paths.
    /// </summary>
    [DisallowMultipleComponent]
    public class DashboardTextBinding : MonoBehaviour
    {
        public TMP_Text label;
        public BatteryField field;
        [Tooltip("C# numeric format, e.g. F2, F1, 0.")]
        public string format = "F2";
        [Tooltip("Appended after the value, e.g. ' V' or ' °C'.")]
        public string unit = "";
        [Tooltip("Optional colour override. Alpha 0 disables.")]
        public Color colorOverride = new Color(1, 1, 1, 0);

        void Awake()
        {
            if (colorOverride.a > 0f && label != null) label.color = colorOverride;
        }

        public void Apply(BatteryReading r)
        {
            if (label == null) return;
            label.text = field switch
            {
                BatteryField.Voltage     => Fmt(r.Voltage) + unit,
                BatteryField.Current     => Fmt(r.Current) + unit,
                BatteryField.Temperature => Fmt(r.Temperature) + unit,
                BatteryField.SOC         => Fmt(r.SOC) + unit,
                BatteryField.SOH         => Fmt(r.SOH) + unit,
                BatteryField.Cycles      => r.Cycles.ToString() + unit,
                BatteryField.Power       => Fmt(r.Power) + unit,
                BatteryField.Status      => r.Status,
                BatteryField.CellId      => r.CellId ?? "",
                BatteryField.Time        => r.Timestamp.ToString("HH:mm:ss"),
                _ => label.text,
            };
        }

        string Fmt(float v)
        {
            try { return v.ToString(string.IsNullOrEmpty(format) ? "F2" : format); }
            catch (FormatException) { return v.ToString("F2"); }
        }
    }
}
