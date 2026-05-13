using BatteryTwin.Data;
using UnityEngine;

namespace BatteryTwin.Bindings
{
    /// <summary>
    /// Drives one of the three small desk cylinders (green/red/orange) so it
    /// encodes a single battery metric (V / I / T) two ways at once:
    ///   - vertical fill: cylinder height lerps between minHeight..maxHeight
    ///   - colour tint: material _BaseColor sampled from colorRamp at the
    ///                  normalised metric value
    ///
    /// Height is applied to <see cref="target"/>.localScale.y, and the cylinder
    /// is anchored to <see cref="baseY"/> so it grows upward from the desk
    /// rather than centring on its pivot.
    /// </summary>
    [DisallowMultipleComponent]
    public class IndicatorCellBinding : MonoBehaviour
    {
        public BatteryField field = BatteryField.Voltage;
        public Transform target;
        public Renderer targetRenderer;

        [Header("Value range")]
        public float minValue = 2.5f;
        public float maxValue = 4.2f;
        [Tooltip("If true, the binding uses Mathf.Abs(value) before normalising. Useful for Current which can be negative.")]
        public bool useAbsoluteValue = false;

        [Header("Height (metres)")]
        public float minHeight = 0.04f;
        public float maxHeight = 0.14f;
        [Tooltip("World/local Y of the desk surface. Cylinder is positioned so its base sits here.")]
        public float baseY = 0.79f;

        [Header("Colour")]
        public Gradient colorRamp;
        public string colorProperty = "_BaseColor";

        MaterialPropertyBlock _mpb;
        int _propId;

        void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            _propId = Shader.PropertyToID(colorProperty);
            if (colorRamp == null || colorRamp.colorKeys == null || colorRamp.colorKeys.Length == 0)
            {
                colorRamp = DefaultRamp();
            }
        }

        public void Apply(BatteryReading reading)
        {
            if (target == null) return;

            float raw = field switch
            {
                BatteryField.Voltage     => reading.Voltage,
                BatteryField.Current     => reading.Current,
                BatteryField.Temperature => reading.Temperature,
                BatteryField.SOC         => reading.SOC,
                BatteryField.SOH         => reading.SOH,
                BatteryField.Power       => reading.Power,
                _ => reading.Voltage,
            };

            float v = useAbsoluteValue ? Mathf.Abs(raw) : raw;
            float t = Mathf.InverseLerp(minValue, maxValue, v);

            // A Unity primitive cylinder is 2 m tall (localScale.y = 1 -> 2m height).
            // Treat localScale.y directly as half-height and anchor base to baseY.
            float halfHeight = Mathf.Lerp(minHeight, maxHeight, t);
            Vector3 s = target.localScale;
            s.y = halfHeight;
            target.localScale = s;

            Vector3 p = target.position;
            p.y = baseY + halfHeight;
            target.position = p;

            if (targetRenderer != null && colorRamp != null)
            {
                Color c = colorRamp.Evaluate(t);
                targetRenderer.GetPropertyBlock(_mpb);
                _mpb.SetColor(_propId, c);
                targetRenderer.SetPropertyBlock(_mpb);
            }
        }

        static Gradient DefaultRamp()
        {
            var g = new Gradient();
            g.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.30f, 0.90f, 0.40f), 0f),
                    new GradientColorKey(new Color(0.95f, 0.85f, 0.30f), 0.5f),
                    new GradientColorKey(new Color(0.95f, 0.30f, 0.25f), 1f),
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return g;
        }
    }
}
