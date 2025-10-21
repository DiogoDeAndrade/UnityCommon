using UnityEngine;
using UnityEditor;
using UnityEditor.AssetImporters;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using static UC.SubtitleTrack;

namespace UC
{
    [ScriptedImporter(1, "srt")]
    public class SubtitleTrackImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var text = File.ReadAllLines(ctx.assetPath);
            var track = ScriptableObject.CreateInstance<SubtitleTrack>();
            track.lines = ParseSRT(text);

            ctx.AddObjectToAsset("SubtitleTrack", track);
            ctx.SetMainObject(track);
        }

        private static List<SubtitleLine> ParseSRT(string[] lines)
        {
            List<SubtitleLine> result = new List<SubtitleLine>();
            Regex timeRegex = new Regex(@"(\d{2}):(\d{2}):(\d{2}),(\d{3})");

            int i = 0;
            while (i < lines.Length)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                {
                    i++;
                    continue;
                }

                // Line with timecodes
                string timeLine = lines[i + 1];
                var match = timeRegex.Matches(timeLine);
                if (match.Count < 2)
                {
                    i++;
                    continue;
                }

                float start = TimeFromMatch(match[0]);
                float end = TimeFromMatch(match[1]);

                i += 2;
                string text = "";
                while (i < lines.Length && !string.IsNullOrWhiteSpace(lines[i]))
                {
                    text += lines[i] + "\n";
                    i++;
                }

                result.Add(new SubtitleLine
                {
                    startTime = start,
                    endTime = end,
                    text = text.Trim()
                });
            }

            return result;
        }

        private static float TimeFromMatch(Match m)
        {
            int hours = int.Parse(m.Groups[1].Value);
            int minutes = int.Parse(m.Groups[2].Value);
            int seconds = int.Parse(m.Groups[3].Value);
            int milliseconds = int.Parse(m.Groups[4].Value);
            return hours * 3600 + minutes * 60 + seconds + milliseconds / 1000f;
        }
    }
}