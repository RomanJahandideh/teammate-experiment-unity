using Teammate.Networking;
using Teammate.Physio;
using TMPro;
using UnityEngine;

namespace Teammate.Interface
{
    /// <summary>
    /// One teammate's status panel entry (role label + halo / numeric readout), matching
    /// the three Figure 1 mockups: No Cue shows nothing, Numeric Cue shows "HR xx bpm /
    /// HRV xx ms" as text, Icon Cue shows the animated halo from TeammateHaloRenderer.
    /// Bind one of these per teammate slot in the team status bar and call Bind() once
    /// their TeammatePhysioNetworkPlayer has spawned.
    /// </summary>
    public class TeammateStatusWidget : MonoBehaviour
    {
        [Header("Sub-views")]
        public GameObject noCueView;
        public GameObject numericCueView;
        public GameObject iconCueView;

        [Header("Icon Cue")]
        public TeammateHaloRenderer haloRenderer;

        [Header("Numeric Cue")]
        public TMP_Text numericText;

        [Header("Common")]
        public TMP_Text roleLabel;

        private TeammatePhysioNetworkPlayer _player;

        public void Bind(TeammatePhysioNetworkPlayer player)
        {
            Unbind();
            _player = player;

            _player.Role.OnValueChanged += HandleRoleChanged;
            _player.DisplayedHrState.OnValueChanged += HandleHrStateChanged;
            _player.DisplayedHrvState.OnValueChanged += HandleHrvStateChanged;
            _player.NumericHrBpmIfDisclosed.OnValueChanged += HandleNumericBpmChanged;
            _player.NumericHrvMsIfDisclosed.OnValueChanged += HandleNumericMsChanged;

            HandleRoleChanged(PlayerRole.Unassigned, _player.Role.Value);
            RefreshIcon();
            RefreshNumeric();

            if (ConditionManager.Instance != null)
            {
                ConditionManager.Instance.OnConditionChanged += ApplyCondition;
                ApplyCondition(ConditionManager.Instance.CurrentCondition.Value);
            }
        }

        public void Unbind()
        {
            if (_player == null) return;

            _player.Role.OnValueChanged -= HandleRoleChanged;
            _player.DisplayedHrState.OnValueChanged -= HandleHrStateChanged;
            _player.DisplayedHrvState.OnValueChanged -= HandleHrvStateChanged;
            _player.NumericHrBpmIfDisclosed.OnValueChanged -= HandleNumericBpmChanged;
            _player.NumericHrvMsIfDisclosed.OnValueChanged -= HandleNumericMsChanged;

            if (ConditionManager.Instance != null)
                ConditionManager.Instance.OnConditionChanged -= ApplyCondition;

            _player = null;
        }

        private void HandleHrStateChanged(HRState previous, HRState current) => RefreshIcon();
        private void HandleHrvStateChanged(HRVState previous, HRVState current) => RefreshIcon();
        private void HandleNumericBpmChanged(float previous, float current) => RefreshNumeric();
        private void HandleNumericMsChanged(float previous, float current) => RefreshNumeric();

        void OnDestroy() => Unbind();

        private void HandleRoleChanged(PlayerRole previous, PlayerRole current)
        {
            if (roleLabel != null) roleLabel.text = current.ToString();
        }

        private void RefreshIcon()
        {
            if (_player == null || haloRenderer == null) return;
            haloRenderer.SetState(_player.DisplayedHrState.Value, _player.DisplayedHrvState.Value);
        }

        private void RefreshNumeric()
        {
            if (_player == null || numericText == null) return;

            float bpm = _player.NumericHrBpmIfDisclosed.Value;
            float ms = _player.NumericHrvMsIfDisclosed.Value;
            numericText.text = bpm > 0f ? $"{bpm:F0} bpm  ·  {ms:F0} ms" : "--";
        }

        private void ApplyCondition(DisplayCondition condition)
        {
            if (noCueView != null) noCueView.SetActive(condition == DisplayCondition.NoCue);
            if (numericCueView != null) numericCueView.SetActive(condition == DisplayCondition.NumericCue);
            if (iconCueView != null) iconCueView.SetActive(condition == DisplayCondition.IconCue);
        }
    }
}
