using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;
using System.IO;

#if UNITY_EDITOR
public class LpcSpriteProcessor : AssetPostprocessor {

	public enum LpcAnimationState
	{
		Hurt,
		Shoot,
		Slash,
		Walk,
		Thrust,
		Spellcast
	}

	private const int LPC_SHEET_WIDTH  = 832;
	private const int LPC_SHEET_HEIGHT = 1344;
	private const int LPC_SPRITE_SIZE  = 64;

	private int m_PixelsPerUnit; // Sets the Pixels Per Unit in the Importer
	private int m_ScFrames;      // Spellcast animation frames
	private int m_ThFrames;      // Thrust animation frames
	private int m_WcFrames;      // Walkcycle animation frames
	private int m_SlFrames;      // Slash animation frames
	private int m_ShFrames;      // Shoot animation frames
	private int m_HuFrames;      // Hurt animation frames

	private bool m_ImportEmptySprites;
	private int m_ColCount;
	private int m_RowCount;

	void RetrieveSettings(){
		// Retrieve Basic Settings
		m_ImportEmptySprites = LpcSpriteSettings.GetImportEmptySprites();
		m_PixelsPerUnit = LpcSpriteSettings.GetPixelsPerUnit ();

		// Retrieve Animation Settings
		m_ScFrames = LpcSpriteSettings.GetScFrameCount();
		m_ThFrames = LpcSpriteSettings.GetThFrameCount();
		m_WcFrames = LpcSpriteSettings.GetWcFrameCount();
		m_SlFrames = LpcSpriteSettings.GetSlFrameCount();
		m_ShFrames = LpcSpriteSettings.GetShFrameCount();
		m_HuFrames = LpcSpriteSettings.GetHuFrameCount();

		// Retrieve Other Settings
		m_ColCount = LpcSpriteSettings.GetColCount();
		m_RowCount = LpcSpriteSettings.GetRowCount();
	}

	void OnPreprocessTexture()
	{
        string filename = Path.GetFileName(assetPath);
        if (filename.Length > 4)
        {
            if (filename.Substring(0, 4) != "LPC_") return;
        }

        // Can't load texture yet, asset does not exist at this stage
		/*Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        if (texture == null)
        {
            var assetType = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
            Debug.LogError("Not a texture (" + assetType + ") [" + assetPath + "]");
            return;
        }
        if (!IsLpcSpriteSheet(texture))
			return;
		*/

		RetrieveSettings ();
		TextureImporter textureImporter = (TextureImporter)assetImporter;
		textureImporter.textureType = TextureImporterType.Sprite;
		textureImporter.spriteImportMode = SpriteImportMode.Multiple;
        textureImporter.mipmapEnabled = false;
		textureImporter.filterMode = FilterMode.Point;
		textureImporter.spritePixelsPerUnit = m_PixelsPerUnit;
        textureImporter.textureCompression = TextureImporterCompression.Uncompressed;
    }

	public void OnPostprocessTexture (Texture2D texture)
	{
        string filename = Path.GetFileName(assetPath);
        if (filename.Length > 4)
        {
            if (filename.Substring(0, 4) != "LPC_") return;
        }

        // Do nothing if it not a LPC Based Sprite
        if (!IsLpcSpriteSheet (texture))
			return;

		Debug.Log ("Importing LPC Character Sheet");
		List<SpriteMetaData> metas = new List<SpriteMetaData>();
		for (int row = 0; row < m_RowCount; ++row)
		{
			for (int col = 0; col < m_ColCount; ++col)
			{
				SpriteMetaData meta = new SpriteMetaData();
				meta.rect = new Rect(col * LPC_SPRITE_SIZE, row * LPC_SPRITE_SIZE, LPC_SPRITE_SIZE, LPC_SPRITE_SIZE);
                meta.alignment = (int)SpriteAlignment.Custom;
                meta.pivot = new Vector2(0.5f, 0.0f);

                LpcAnimationState animState = GetAnimationState (row);

				if (!m_ImportEmptySprites) {
					if ((animState == LpcAnimationState.Hurt && col >= m_HuFrames))
						break;
					if ((animState == LpcAnimationState.Shoot && col >= m_ShFrames))
						break;
					if ((animState == LpcAnimationState.Slash && col >= m_SlFrames))
						break;
					if ((animState == LpcAnimationState.Thrust && col >= m_ThFrames))
						break;
					if ((animState == LpcAnimationState.Walk && col >= m_WcFrames))
						break;
					if ((animState == LpcAnimationState.Spellcast && col >= m_ScFrames))
						break;
				}

				string namePrefix = ResolveLpcNamePrefix (row);
				meta.name = namePrefix + col;
				metas.Add(meta);
			}
		}
		TextureImporter textureImporter = (TextureImporter)assetImporter;
		textureImporter.spritesheet = metas.ToArray();
    }

	public void OnPostprocessSprites(Texture2D texture, Sprite[] sprites)
	{

    }

	// Check if a texture is a LPC Spritesheet by
	// checking the textures width and height
	private bool IsLpcSpriteSheet(Texture2D texture)
	{
        if (texture.width == LPC_SHEET_WIDTH
            && texture.height == LPC_SHEET_HEIGHT)
        {
            return true;
        }

        return false;
	}

	public static LpcAnimationState GetAnimationState(int row)
	{
		switch (row) {
		case(0):
			return LpcAnimationState.Hurt;
		case(1):
		case(2):
		case(3):
		case(4):
			return LpcAnimationState.Shoot;
		case(5):
		case(6):
		case(7):
		case(8):
			return LpcAnimationState.Slash;
		case(9):
		case(10):
		case(11):
		case(12):
			return LpcAnimationState.Walk;
		case(13):
		case(14):
		case(15):
		case(16):
			return LpcAnimationState.Thrust;
		case(17):
		case(18):
		case(19):
		case(20):
			return LpcAnimationState.Spellcast;
		default:
			Debug.LogError ("GetAnimationState unknown row: " + row);
			return 0;
		}
	}

	private string ResolveLpcNamePrefix(int row)
	{
		switch (row) {
		case(0):
			return "HurtS_";
		case(1):
			return "ShootE_";
		case(2):
			return "ShootS_";
		case(3):
			return "ShootW_";
		case(4):
			return "ShootN_";
		case(5):
			return "SlashE_";
		case(6):
			return "SlashS_";
		case(7):
			return "SlashW_";
		case(8):
			return "SlashN_";
		case(9):
			return "WalkE_";
		case(10):
			return "WalkS_";
		case(11):
			return "WalkW_";
		case(12):
			return "WalkN_";
		case(13):
			return "ThrustE_";
		case(14):
			return "ThrustS_";
		case(15):
			return "ThrustW_";
		case(16):
			return "ThrustN_";
		case(17):
			return "CastE_";
		case(18):
			return "CastS_";
		case(19):
			return "CastW_";
		case(20):
			return "CastN_";
		default:
			Debug.LogError ("ResolveLpcNamePrefix unknown row: " + row);
			return "";
		}
	}

}
#endif