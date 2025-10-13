using System;
using TMPro;
using UnityEngine;

public class VersionText : MonoBehaviour
{
    TextMeshProUGUI versionText;

    void Start()
    {


        versionText = GetComponent<TextMeshProUGUI>();
        if (versionText)
        {
            if (versionText.text.IndexOf("{0}") == -1)
            {
                versionText.text = $"V{Application.version}";
            }
            else
            {
                versionText.text = string.Format(versionText.text, Application.version, GetBuildDate());
            }
        }

        Destroy(this);
    }

    public static string GetBuildDate()
    {
        var buildInfo = Resources.Load<TextAsset>("BuildInfo");
        if (buildInfo)
        {
            return buildInfo.text;
        }

        return DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'");
    }
}
