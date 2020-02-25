using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class SineWaveGenerator : MonoBehaviour
{
    public float frequency = 440;
    public float volume = 1.0f;

    float   current = 0;
    float   outputSampleRate;

    private void Awake()
    {
        outputSampleRate = AudioSettings.outputSampleRate;
    }

    void OnAudioFilterRead(float[] data, int channels)
    {
        float timeOffset = frequency * 2.0f * Mathf.PI / outputSampleRate;

        for (int i = 0; i < data.Length; i += channels)
        {
            for (int j = 0; j < channels; j++)
            {
                data[i + j] = volume * Mathf.Sin(current);
            }

            current += timeOffset;
        }
    }
}
