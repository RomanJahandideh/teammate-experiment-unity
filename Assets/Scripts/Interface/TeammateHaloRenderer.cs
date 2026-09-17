using Teammate.Physio;
using UnityEngine;
using UnityEngine.UI;

namespace Teammate.Interface
{
    /// <summary>
    /// Renders one teammate's role icon halo per the Icon Cue design: HR drives pulse
    /// speed and glow ("pulse" and "glow"), HRV drives ring coherence ("ring"), with no
    /// numbers or emotion words. Encodings follow the stimulus table exactly:
    ///
    /// HR1 slow pulse, soft glow — HR2 medium pulse, clearer glow —
    /// HR3 fast pulse, stronger glow — HR4 rapid pulse, sharp glow, stronger expansion.
    ///
    /// V1 smooth continuous ring — V2 clean dotted ring —
    /// V3 denser segmented ring with a slight wobble —
    /// V4 broken ring, visible jitter, irregular opacity.
    ///
    /// Attach to a role-icon UI prefab with a core glow Image and either a hand-placed
    /// ring of segment Images, or a segmentPrefab to auto-build the ring at runtime.
    /// </summary>
    public class TeammateHaloRenderer : MonoBehaviour
    {
        [Header("Core (HR: pulse + glow)")]
        public Image coreGlow;
        public Color glowColor = new Color(1f, 0.55f, 0.35f, 1f);

        [Header("Ring (HRV: coherence)")]
        [Tooltip("Pre-placed ring segments. Leave empty to auto-build from segmentPrefab.")]
        public Image[] ringSegments;
        public RectTransform segmentPrefab;
        public RectTransform ringRoot;
        [Range(6, 32)] public int autoSegmentCount = 16;
        public float ringRadius = 60f;

        // --- HR -> pulse speed (cycles/sec) and peak glow alpha/scale ---
        private static readonly float[] PulseHz = { 0.6f, 1.0f, 1.6f, 2.4f };
        private static readonly float[] GlowPeakAlpha = { 0.35f, 0.55f, 0.75f, 1.0f };
        private static readonly float[] ScaleExpansion = { 0.06f, 0.10f, 0.16f, 0.26f }; // added to base scale at pulse peak

        // --- HRV -> fraction of ring segments lit, and jitter amplitude (degrees) ---
        private static readonly float[] SegmentVisibleFraction = { 1.00f, 0.60f, 0.45f, 0.30f };
        private static readonly float[] JitterDegrees = { 0f, 0f, 4f, 14f };
        private static readonly float[] OpacityFlickerAmount = { 0f, 0f, 0.10f, 0.45f };

        private HRState _hr = HRState.HR1_RestingLowActivation;
        private HRVState _hrv = HRVState.V1_HighCoherence;
        private Vector3 _coreBaseScale = Vector3.one;
        private float[] _segmentSeeds;
        private float[] _segmentBaseZ;

        void Awake()
        {
            if (coreGlow != null)
            {
                _coreBaseScale = coreGlow.rectTransform.localScale;
                coreGlow.color = glowColor;
            }

            if ((ringSegments == null || ringSegments.Length == 0) && segmentPrefab != null && ringRoot != null)
                BuildRingSegments();

            int count = ringSegments?.Length ?? 0;
            _segmentSeeds = new float[count];
            _segmentBaseZ = new float[count];
            for (int i = 0; i < count; i++)
            {
                _segmentSeeds[i] = Random.value * 1000f;
                _segmentBaseZ[i] = ringSegments[i] != null ? ringSegments[i].transform.localEulerAngles.z : 0f;
            }
        }

        private void BuildRingSegments()
        {
            ringSegments = new Image[autoSegmentCount];
            for (int i = 0; i < autoSegmentCount; i++)
            {
                float angle = 360f * i / autoSegmentCount;
                var seg = Instantiate(segmentPrefab, ringRoot);
                seg.anchoredPosition = new Vector2(
                    ringRadius * Mathf.Sin(angle * Mathf.Deg2Rad),
                    ringRadius * Mathf.Cos(angle * Mathf.Deg2Rad));
                seg.localRotation = Quaternion.Euler(0, 0, -angle);
                ringSegments[i] = seg.GetComponent<Image>();
            }
        }

        /// <summary>Called by TeammateStatusWidget whenever the replicated displayed state changes.</summary>
        public void SetState(HRState hr, HRVState hrv)
        {
            _hr = hr;
            _hrv = hrv;
        }

        void Update()
        {
            AnimateCore();
            AnimateRing();
        }

        private void AnimateCore()
        {
            if (coreGlow == null) return;

            int hrIndex = (int)_hr;
            float hz = PulseHz[hrIndex];
            float peakAlpha = GlowPeakAlpha[hrIndex];
            float expansion = ScaleExpansion[hrIndex];

            // 0..1 pulse phase, eased so it reads as a heartbeat rather than a pure sine.
            float phase = Mathf.Repeat(Time.time * hz, 1f);
            float pulse = Mathf.Pow(Mathf.Sin(phase * Mathf.PI), 2f);

            var c = glowColor;
            c.a = Mathf.Lerp(peakAlpha * 0.35f, peakAlpha, pulse);
            coreGlow.color = c;
            coreGlow.rectTransform.localScale = _coreBaseScale * (1f + expansion * pulse);
        }

        private void AnimateRing()
        {
            if (ringSegments == null || ringSegments.Length == 0) return;

            int hrvIndex = (int)_hrv;
            float visibleFraction = SegmentVisibleFraction[hrvIndex];
            float jitterDeg = JitterDegrees[hrvIndex];
            float flicker = OpacityFlickerAmount[hrvIndex];
            int totalVisible = Mathf.Max(1, Mathf.RoundToInt(ringSegments.Length * visibleFraction));

            for (int i = 0; i < ringSegments.Length; i++)
            {
                var seg = ringSegments[i];
                if (seg == null) continue;

                // V1/V2: deterministic even spacing (smooth ring / clean dotted ring).
                // V3/V4: same spacing rule, with per-segment flicker/jitter layered on below.
                bool lit = (i % Mathf.Max(1, ringSegments.Length / totalVisible)) == 0;

                float seed = _segmentSeeds[i];
                float noise = Mathf.PerlinNoise(seed, Time.time * 1.5f) * 2f - 1f;

                var color = seg.color;
                color.a = lit ? Mathf.Clamp01(1f - flicker * 0.5f + noise * flicker) : 0f;
                seg.color = color;

                if (jitterDeg > 0f)
                {
                    seg.transform.localRotation = Quaternion.Euler(0, 0, _segmentBaseZ[i] + noise * jitterDeg);
                }
            }
        }
    }
}
