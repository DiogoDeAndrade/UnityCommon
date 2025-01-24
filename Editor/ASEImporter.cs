using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.AssetImporters;
using System;

[ScriptedImporter(1, "asep")] // 1 is the version, "asep" is the file extension - this should be a .ase importer, but that conflicts with ASE sprite, so we require palette files from Photoshop to have the "asep" extension
public class ASEImporter : ScriptedImporter
{
    public override void OnImportAsset(AssetImportContext ctx)
    {
        // Read the ASE file
        List<string> names = new();

        string path = ctx.assetPath;
        var colors = ParseASEFile(path, names);

        if (colors == null || colors.Count == 0)
        {
            Debug.LogError($"ASE Swatch Importer: Failed to parse ASE file: {path}");
            return;
        }

        // Create a new ColorPalette ScriptableObject
        ColorPalette palette = ScriptableObject.CreateInstance<ColorPalette>();
        for (int i = 0; i < colors.Count; i++)
        {
            palette.Add(names[i], colors[i]);
        }

        // Add the ScriptableObject to the import context
        ctx.AddObjectToAsset("ColorPalette", palette);
        ctx.SetMainObject(palette);
    }

    public static short SwapInt16(short value)
    {
        return (short)((value << 8) | ((value >> 8) & 0xFF));
    }
    public static ushort SwapUInt16(ushort value)
    {
        return (ushort)((value << 8) | ((value >> 8) & 0xFF));
    }

    public static int SwapInt32(int value)
    {
        return (int)(((uint)(value & 0x000000FF) << 24) |
                     ((uint)(value & 0x0000FF00) << 8) |
                     ((uint)(value & 0x00FF0000) >> 8) |
                     ((uint)(value & 0xFF000000) >> 24));
    }

    public static float SwapFloat(float value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        Array.Reverse(bytes);
        return BitConverter.ToSingle(bytes, 0);
    }

    private List<Color> ParseASEFile(string path, List<string> names)
    {
        var colors = new List<Color>();

        if (names != null) names.Clear();

        try
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
            using (var reader = new BinaryReader(stream))
            {
                // Check ASE header
                string signature = new string(reader.ReadChars(4));
                if (signature != "ASEF")
                {
                    Debug.LogError("ASE Swatch Importer: Invalid ASE file format.");
                    return null;
                }

                // Parse the ASE file
                int majorVersion = SwapInt16(reader.ReadInt16()); 
                int minorVersion = SwapInt16(reader.ReadInt16());
                int chunkCount = SwapInt32(reader.ReadInt32());

                for (int i = 0; i < chunkCount; i++)
                {
                    ushort chunkType = SwapUInt16(reader.ReadUInt16());
                    int chunkSize = SwapInt32(reader.ReadInt32());

                    if (chunkType == 0x0001) // Color entry
                    {
                        int nameLength = SwapUInt16(reader.ReadUInt16()) * 2;
                        byte[] nameBytes = reader.ReadBytes(nameLength);
                        for (int j = 0; j < nameBytes.Length; j += 2)
                        {
                            byte temp = nameBytes[j];
                            nameBytes[j] = nameBytes[j + 1];
                            nameBytes[j + 1] = temp;
                        }
                        string name = System.Text.Encoding.Unicode.GetString(nameBytes).TrimEnd('\0');

                        string colorSpace = new string(reader.ReadChars(4));    
                        if (colorSpace.StartsWith("RGB"))
                        {
                            float r = SwapFloat(reader.ReadSingle());
                            float g = SwapFloat(reader.ReadSingle());
                            float b = SwapFloat(reader.ReadSingle());
                            float a = 1.0f; // ASE files often don't have alpha; assume full opacity

                            colors.Add(new Color(r, g, b, a));
                            names?.Add(name);
                        }
                        else
                        {
                            Debug.LogError($"ASE Swatch Importer: No support for colorspace {colorSpace}!");
                            return null;
                        }

                        int endOfDataMarker = SwapUInt16(reader.ReadUInt16());
                        if ((endOfDataMarker != 0x0000) && (endOfDataMarker != 0x0002))
                        {
                            Debug.LogError($"ASE Swatch Importer: Format error - invalid end of data marker [{endOfDataMarker}]!");
                            return null;
                        }
                    }
                    else
                    {
                        // Skip unknown chunks
                        reader.BaseStream.Seek(chunkSize, SeekOrigin.Current);
                    }
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"ASE Swatch Importer: Failed to parse ASE file: {ex.Message}");
            return null;
        }

        if (colors.Count == 0)
        {
            Debug.LogError("ASE Swatch Importer: No colors found!");
        }

        return colors;
    }
}
