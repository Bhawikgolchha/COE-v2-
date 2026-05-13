using BatteryTwin.Data;
using UnityEngine;

namespace BatteryTwin.Bindings
{
    /// <summary>
    /// Lerps the target Renderer's _BaseColor from coolColor to hotColor over
    /// [minTemp, maxTemp]. Uses a MaterialPropertyBlock so we don't mint a
    /// material instance per cell.
    /// </summary>
    [DisallowMultipleComponent]
    public class TemperatureColorBinding : MonoBehaviour
    {
        public Renderer target;
        public float minTemp = 20f;
        public float maxTemp = 55f;
        public Color coolColor = new Color(0.30f, 0.60f, 1.00f);
        public Color hotColor  = new Color(1.00f, 0.25f, 0.20f);

        [Tooltip("Shader property to write. URP/Lit uses _BaseColor; Built-in Standard uses _Color.")]
        public string colorProperty = "_BaseColor";

        MaterialPropertyBlock _mpb;
        int _propId;

        void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            _propId = Shader.PropertyToID(colorProperty);
        }

        public void Apply(BatteryReading reading)
        {
            if (target == null) return;
            float k = Mathf.InverseLerp(minTemp, maxTemp, reading.Temperature);
            Color c = Color.Lerp(coolColor, hotColor, k);
            target.GetPropertyBlock(_mpb);
            _mpb.SetColor(_propId, c);
            target.SetPropertyBlock(_mpb);
        }
    }
}
