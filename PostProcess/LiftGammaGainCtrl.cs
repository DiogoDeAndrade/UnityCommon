using NaughtyAttributes;
using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class LiftGammaGainCtrl: MonoBehaviour
{
    public enum Mode { Manual, PlayerPrefRead, PlayerPrefReadWrite };

    [SerializeField] private Volume volume;
    [SerializeField] private Mode liftMode;
    [SerializeField, HideIf("liftMode", Mode.Manual)] private string liftKey = "Lift";
    [SerializeField] private Mode gammaMode;
    [SerializeField, HideIf("gammaMode", Mode.Manual)] private string gammaKey= "Gamma";
    [SerializeField] private Mode gainMode;
    [SerializeField, HideIf("gainMode", Mode.Manual)] private string gainKey = "Gain";
    [SerializeField] bool updateAllFrames = false;

    private LiftGammaGain liftGammaGain;

    void Start()
    {
        if (volume == null)
        {
            volume = GetComponent<Volume>();
            if (volume == null)
            {
                volume = FindFirstObjectByType<Volume>();
            }
        }
        // Check if the Volume has a profile and if it contains a ColorAdjustments override
        if (volume != null && volume.profile.TryGet<LiftGammaGain>(out liftGammaGain))
        {
        }
        else
        {
            Debug.LogError("No LiftGammaGain override found in the Volume Profile!");
        }

        UpdateValues();
    }

    private void Update()
    {
        if (updateAllFrames) UpdateValues();
    }

    public void UpdateValues()
    {
        SetFromPlayerPrefs(liftMode, liftKey, Vector4.zero, SetLift);
        SetFromPlayerPrefs(gammaMode, gammaKey, Vector4.zero, SetGamma);
        SetFromPlayerPrefs(gainMode, gainKey, Vector4.zero, SetGain);
    }

    private void SetFromPlayerPrefs(Mode mode, string key, Vector4 defaultValue, Action<Vector4, bool> function)
    {
        if (mode == Mode.Manual) return;

        if (string.IsNullOrEmpty(key)) return;

        Vector4 val = PlayerPrefsHelpers.GetVector4(key, defaultValue);

        function(val, false);
    }

    void SetInPlayerPrefs(Mode mode, string key, Vector4 value)
    {
        if (mode == Mode.Manual) return;

        if (string.IsNullOrEmpty(key)) return;

        PlayerPrefsHelpers.SetVector4(key, value);
        PlayerPrefs.Save();
    }

    public void SetLift(Vector4 value, bool alsoSet = true)
    {
        if (liftGammaGain != null)
        {
            liftGammaGain.lift.Override(value);
            if (alsoSet) SetInPlayerPrefs(liftMode, liftKey, value);
        }
    }

    public void SetGamma(Vector4 value, bool alsoSet = true)
    {
        if (liftGammaGain != null)
        {
            liftGammaGain.gamma.Override(value);
            if (alsoSet) SetInPlayerPrefs(gammaMode, gammaKey, value);
        }
    }

    public void SetGain(Vector4 value, bool alsoSet = true)
    {
        if (liftGammaGain != null)
        {
            liftGammaGain.gain.Override(value);
            if (alsoSet) SetInPlayerPrefs(gainMode, gainKey, value);
        }
    }
}
