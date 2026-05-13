using System;
using TMPro;
using UnityEngine;

namespace BatteryTwin.Twin
{
    /// <summary>
    /// Wall-clock display, independent of data source. Two TMP fields: one
    /// for time, one for date. Updates at the configured rate (default 1 Hz).
    /// </summary>
    public class TimePanel : MonoBehaviour
    {
        public TMP_Text timeLabel;
        public TMP_Text dateLabel;
        public float updateInterval = 1.0f;

        float _next;

        void Update()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + updateInterval;

            DateTime now = DateTime.Now;
            if (timeLabel != null) timeLabel.text = now.ToString("HH:mm:ss");
            if (dateLabel != null) dateLabel.text = now.ToString("dd MMM yyyy");
        }
    }
}
