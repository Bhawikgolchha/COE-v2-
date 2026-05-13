using System.Collections.Generic;
using BatteryTwin.Data;
using UnityEngine;
using UnityEngine.UI;

namespace BatteryTwin.Bindings
{
    /// <summary>
    /// Tiny rolling line chart on a UI Graphic. Stores up to `capacity` samples
    /// in a ring buffer; rebuilds the mesh as a series of thick line segments
    /// across the rect. One chart, one field — drop three into the dashboard
    /// canvas for V/I/T.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class RollingLineChart : Graphic
    {
        public BatteryField field = BatteryField.Voltage;
        public int capacity = 120;
        public float yMin = 0f;
        public float yMax = 5f;
        public float lineThickness = 2f;
        public Color lineColor = Color.green;

        readonly Queue<float> _samples = new();

        public void PushSample(BatteryReading r)
        {
            float v = field switch
            {
                BatteryField.Voltage     => r.Voltage,
                BatteryField.Current     => r.Current,
                BatteryField.Temperature => r.Temperature,
                BatteryField.SOC         => r.SOC,
                BatteryField.SOH         => r.SOH,
                BatteryField.Power       => r.Power,
                _ => 0f,
            };
            _samples.Enqueue(v);
            while (_samples.Count > Mathf.Max(2, capacity)) _samples.Dequeue();
            SetVerticesDirty();
        }

        public void Clear()
        {
            _samples.Clear();
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_samples.Count < 2) return;

            Rect r = rectTransform.rect;
            int n = _samples.Count;
            int cap = Mathf.Max(2, capacity);

            // Convert the queue to an indexed array on the fly.
            int i = 0;
            float[] buf = new float[n];
            foreach (var s in _samples) buf[i++] = s;

            float xStep = r.width / (cap - 1);
            float yRange = Mathf.Max(0.0001f, yMax - yMin);
            float t = lineThickness * 0.5f;

            // Right-align the trace so the latest sample sits at the right edge.
            float xOffset = r.xMin + (cap - n) * xStep;

            for (int k = 0; k < n - 1; k++)
            {
                float xA = xOffset + k * xStep;
                float xB = xOffset + (k + 1) * xStep;
                float yA = r.yMin + Mathf.Clamp01((buf[k]     - yMin) / yRange) * r.height;
                float yB = r.yMin + Mathf.Clamp01((buf[k + 1] - yMin) / yRange) * r.height;

                Vector2 dir = new Vector2(xB - xA, yB - yA).normalized;
                Vector2 perp = new Vector2(-dir.y, dir.x) * t;

                int idx = vh.currentVertCount;
                vh.AddVert(new Vector3(xA - perp.x, yA - perp.y), lineColor, Vector2.zero);
                vh.AddVert(new Vector3(xA + perp.x, yA + perp.y), lineColor, Vector2.zero);
                vh.AddVert(new Vector3(xB + perp.x, yB + perp.y), lineColor, Vector2.zero);
                vh.AddVert(new Vector3(xB - perp.x, yB - perp.y), lineColor, Vector2.zero);
                vh.AddTriangle(idx + 0, idx + 1, idx + 2);
                vh.AddTriangle(idx + 0, idx + 2, idx + 3);
            }
        }
    }
}
