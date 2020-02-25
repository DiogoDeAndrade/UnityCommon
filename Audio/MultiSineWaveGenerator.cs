using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class MultiSineWaveGenerator : MonoBehaviour
{
    [System.Serializable]
    public struct Sine
    {
        public bool     enable;
        public double   frequency;
        public float    volume;
    }
    public float  globalVolume = 1.0f;
    public Sine[] sines;

    double[]    phase;
    float       outputSampleRate;

    private void Awake()
    {
        phase = new double[sines.Length];
        for (int i = 0; i < sines.Length; i++) phase[i] = 0;

        outputSampleRate = AudioSettings.outputSampleRate;
    }

    void OnAudioFilterRead(float[] data, int channels)
    {
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = 0;
        }

        double val = 0;

        for (int i = 0; i < data.Length; i += channels)
        {
            val = 0;
            for (int k = 0; k < sines.Length; k++)
            {
                if (!sines[k].enable) continue;

                val += sines[k].volume * Math.Sin(phase[k]);

                phase[k] += sines[k].frequency * 2.0f * Mathf.PI / outputSampleRate;
            }

            val *= globalVolume;
            for (int j = 0; j < channels; j++)
            {
                data[i + j] = (float)val;
            }
        }
    }
}
