using System.Collections.Generic;
using UnityEngine;

public static class StringExtensions 
{
    public static string FindReplace(this string input, Dictionary<string,string> translator)
    {
        string txt = input;
        if (translator != null)
        {
            foreach (var t in translator)
            {
                txt = txt.Replace(t.Key, t.Value);
            }
        }

        return txt;
    }
}
