using BatteryTwin.Data;
using UnityEngine;
using UnityEngine.UI;

namespace BatteryTwin.Bindings
{
    /// <summary>
    /// Radial / linear fill gauge driven by a single field. The fillImage's
    /// Image.fillAmount goes 0..1 across [min, max], and the colour is taken
    /// from the optional gradient at the same t.
    /// </summary>
    [DisallowMultipleComponent]
    public class GaugeBinding : MonoBehaviour
    {
        public Image fillImage;
        public BatteryField field = BatteryField.SOC;
        public float min = 0f;
        public float max = 100f;
        public Gradient fillGradient;
        public bool driveColor = true;

        public void Apply(BatteryReading r)
        {
            if (fillImage == null) return;
            float v = FieldValue(r);
            float t = Mathf.InverseLerp(min, max, v);
            fillImage.fillAmount = t;
            if (driveColor && fillGradient != null && fillGradient.colorKeys.Length > 0)
                fillImage.color = fillGradient.Evaluate(t);
        }

        float FieldValue(BatteryReading r) => field switch
        {
            BatteryField.Voltage     => r.Voltage,
            BatteryField.Current     => r.Current,
            BatteryField.Temperature => r.Temperature,
            BatteryField.SOC         => r.SOC,
            BatteryField.SOH         => r.SOH,
            BatteryField.Cycles      => r.Cycles,
            BatteryField.Power       => r.Power,
            _ => 0f,
        };
    }
}
