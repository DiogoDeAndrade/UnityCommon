using NaughtyAttributes;
using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class ColorAdjustmentCtrl: MonoBehaviour
{
    public enum Mode { Manual, PlayerPrefRead, PlayerPrefReadWrite };

    [SerializeField] private Volume volume;
    [SerializeField] private Mode exposureMode;
    [SerializeField, HideIf("exposureMode", Mode.Manual)] private string exposureKey = "Exposure";
    [SerializeField] private Mode contrastMode;
    [SerializeField, HideIf("contrastMode", Mode.Manual)] private string contrastKey = "Contrast";
    [SerializeField] private Mode hueShiftMode;
    [SerializeField, HideIf("hueShiftMode", Mode.Manual)] private string hueShiftKey = "HueShift";
    [SerializeField] private Mode saturationMode;
    [SerializeField, HideIf("saturationMode", Mode.Manual)] private string saturationKey = "Saturation";
    [SerializeField] bool updateAllFrames = false;

    private ColorAdjustments colorAdjustments;

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
        if (volume != null && volume.profile.TryGet<ColorAdjustments>(out colorAdjustments))
        {
            Debug.Log("ColorAdjustments found in Volume Profile!");
        }
        else
        {
            Debug.LogError("No ColorAdjustments override found in the Volume Profile!");
        }

        UpdateValues();
    }

    private void Update()
    {
        if (updateAllFrames) UpdateValues();
    }

    public void UpdateValues()
    {
        SetValue(exposureMode, exposureKey, 0.0f, SetPostExposure);
        SetValue(contrastMode, contrastKey, 0.0f, SetContrast);
        SetValue(hueShiftMode, hueShiftKey, 0.0f, SetHueShift);
        SetValue(saturationMode, saturationKey, 0.0f, SetSaturation);
    }

    private void SetValue(Mode mode, string key, float defaultValue, Action<float, bool> function)
    {
        if (mode == Mode.Manual) return;

        if (string.IsNullOrEmpty(key)) return;

        float value = PlayerPrefs.GetFloat(key, defaultValue);

        function(value, false);
    }
    private void SetValue(Mode mode, string key, float value)
    {
        if (mode == Mode.Manual) return;

        if (string.IsNullOrEmpty(key)) return;

        PlayerPrefs.SetFloat(key, value);
    }

    public void SetPostExposure(float value, bool alsoSet = true)
    {
        if (colorAdjustments != null)
        {
            colorAdjustments.postExposure.value = value;
            if (alsoSet) SetValue(exposureMode, exposureKey, value);
        }
    }

    public void SetContrast(float value, bool alsoSet = true)
    {
        if (colorAdjustments != null)
        {
            colorAdjustments.contrast.value = value;
            if (alsoSet) SetValue(contrastMode, contrastKey, value);
        }
    }

    public void SetHueShift(float value, bool alsoSet = true)
    {
        if (colorAdjustments != null)
        {
            colorAdjustments.hueShift.value = value;
            if (alsoSet) SetValue(hueShiftMode, hueShiftKey, value);
        }
    }

    public void SetSaturation(float value, bool alsoSet = true)
    {
        if (colorAdjustments != null)
        {
            colorAdjustments.saturation.value = value;
            if (alsoSet) SetValue(saturationMode, saturationKey, value);
        }
    }
}
