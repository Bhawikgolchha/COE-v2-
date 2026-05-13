using BatteryTwin.Data;
using UnityEngine;

namespace BatteryTwin.Bindings
{
    /// <summary>
    /// Drives a ParticleSystem to visualize current flow.
    /// - Sign of current flips emission direction along the local X axis.
    /// - Magnitude scales rateOverTime (more amps = denser flow).
    /// Optional arrow Transform yaws 180° to match the flow direction.
    /// </summary>
    [DisallowMultipleComponent]
    public class CurrentParticleBinding : MonoBehaviour
    {
        public ParticleSystem ps;
        public Transform arrow;
        public float ampsToRate = 3f;
        [Tooltip("Base linear speed of particles (units/sec) at |I|=1A.")]
        public float baseSpeedPerAmp = 0.05f;
        [Tooltip("Threshold below which we treat current as idle and stop emitting.")]
        public float idleThresholdAmps = 0.1f;

        void Reset()
        {
            ps = GetComponent<ParticleSystem>();
        }

        public void Apply(BatteryReading reading)
        {
            if (ps == null) return;

            float sign = Mathf.Sign(reading.Current);
            float mag = Mathf.Abs(reading.Current);

            var emission = ps.emission;
            emission.rateOverTime = (mag < idleThresholdAmps) ? 0f : mag * ampsToRate;

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(sign * baseSpeedPerAmp * Mathf.Max(1f, mag));

            if (arrow != null)
            {
                var e = arrow.localEulerAngles;
                e.y = (sign >= 0f) ? 0f : 180f;
                arrow.localEulerAngles = e;
            }
        }
    }
}
