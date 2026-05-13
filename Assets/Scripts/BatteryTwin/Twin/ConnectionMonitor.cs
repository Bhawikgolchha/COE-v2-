using TMPro;
using UnityEngine;

namespace BatteryTwin.Twin
{
    /// <summary>
    /// Drives the small status panels: Connection (green/red), Data Source
    /// (active source type name), Update Rate (last-known cadence).
    /// "Stale" = no reading received in staleTimeoutSec.
    /// </summary>
    public class ConnectionMonitor : MonoBehaviour
    {
        public TwinController twin;

        [Header("Panels")]
        public TMP_Text connectionLabel;
        public GameObject connectedIcon;
        public GameObject disconnectedIcon;
        public TMP_Text dataSourceLabel;
        public TMP_Text updateRateLabel;

        [Header("Thresholds")]
        [Tooltip("Mark as disconnected after this many seconds of silence.")]
        public float staleTimeoutSec = 3.0f;
        [Tooltip("Display format for update rate, in seconds. e.g. {0:F1} s.")]
        public string updateRateFormat = "{0:F1} s";
        [Tooltip("Optional: pin the displayed update rate. -1 = derive from source if available.")]
        public float displayedUpdateRate = 1.0f;

        bool _lastConnected = true;

        void Start()
        {
            if (dataSourceLabel != null && twin != null)
                dataSourceLabel.text = PrettySourceName(twin.ActiveSourceTypeName);

            if (updateRateLabel != null)
                updateRateLabel.text = string.Format(updateRateFormat, displayedUpdateRate);

            UpdateConnectionVisual(true, force: true);
        }

        void Update()
        {
            if (twin == null) return;
            bool connected = twin.HasReading && twin.SecondsSinceLastReading <= staleTimeoutSec;
            if (connected != _lastConnected)
            {
                UpdateConnectionVisual(connected, force: false);
                _lastConnected = connected;
            }
        }

        void UpdateConnectionVisual(bool connected, bool force)
        {
            if (connectionLabel != null)
            {
                connectionLabel.text = connected ? "Connected" : "Disconnected";
                connectionLabel.color = connected ? new Color(0.40f, 0.92f, 0.45f) : new Color(0.95f, 0.35f, 0.35f);
            }
            if (connectedIcon != null)    connectedIcon.SetActive(connected);
            if (disconnectedIcon != null) disconnectedIcon.SetActive(!connected);
        }

        static string PrettySourceName(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return "(none)";
            if (typeName.StartsWith("Mock")) return "Mock";
            if (typeName.StartsWith("Firebase")) return "Cloud (Firebase)";
            return typeName;
        }
    }
}
