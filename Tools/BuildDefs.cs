using UnityEngine;

[CreateAssetMenu(fileName = "BuildDefs", menuName = "Unity Common/BuildDefs")]
public class BuildDefs : ScriptableObject
{
    public string   version = "1.0.0";
    public string   username = "defaultuser";
    public string   projectName = "";
    public bool     buildWindows = true;
    public bool     buildWeb;
    public bool     createZipFiles = true;
    public bool     uploadToItch = true;

    public bool anyBuilds => buildWindows | buildWeb;
}
