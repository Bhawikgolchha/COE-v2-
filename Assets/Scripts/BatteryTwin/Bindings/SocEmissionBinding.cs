using BatteryTwin.Data;
using UnityEngine;

namespace BatteryTwin.Bindings
{
    /// <summary>
    /// Modulates the target Renderer's emissive HDR colour by SoC%.
    /// Bright at 100%, dark at 0%. URP/Lit needs the _EMISSION keyword on the
    /// material (set in inspector once); we don't toggle it from script
    /// because MaterialPropertyBlocks can't drive keywords.
    /// </summary>
    [DisallowMultipleComponent]
    public class SocEmissionBinding : MonoBehaviour
    {
        public Renderer target;
        [ColorUsage(showAlpha: false, hdr: true)]
        public Color emissionColor = new Color(0.15f, 0.95f, 0.55f);
        public float maxIntensity = 4f;
        public string emissionProperty = "_EmissionColor";

        MaterialPropertyBlock _mpb;
        int _propId;

        void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            _propId = Shader.PropertyToID(emissionProperty);
        }

        public void Apply(BatteryReading reading)
        {
            if (target == null) return;
            float k = Mathf.Clamp01(reading.SOC / 100f) * maxIntensity;
            target.GetPropertyBlock(_mpb);
            _mpb.SetColor(_propId, emissionColor * k);
            target.SetPropertyBlock(_mpb);
        }
    }
}
