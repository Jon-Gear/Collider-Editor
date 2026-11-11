using UnityEditor;
using UnityEngine;

namespace ColliderEditor
{
	public static class ColliderEditorUIHelper
	{

		public static Texture2D[] colliderIcons;

		/// <summary>
		/// Gets all the editor icons and loads them and stores them in an array
		/// </summary>
		public static void GetIcons()
		{
			colliderIcons = new Texture2D[6];
			string[] locs;

			locs = AssetDatabase.FindAssets("ColliderEditorIconSphere t:texture2D");
			if(locs.Length > 0)
			{
				colliderIcons[0] = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(locs[0]));
			}

			locs = AssetDatabase.FindAssets("ColliderEditorIconCapsule t:texture2D");
			if(locs.Length > 0)
			{
				colliderIcons[1] = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(locs[0]));
			}

			locs = AssetDatabase.FindAssets("ColliderEditorIconBox t:texture2D");
			if(locs.Length > 0)
			{
				colliderIcons[2] = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(locs[0]));
			}

			locs = AssetDatabase.FindAssets("ColliderEditorIconConvexMesh t:texture2D");
			if (locs.Length > 0)
			{
				colliderIcons[3] = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(locs[0]));
			}

			locs = AssetDatabase.FindAssets("ColliderEditorIconCylinder t:texture2D");
			if (locs.Length > 0)
			{
				colliderIcons[4] = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(locs[0]));
			}

			locs = AssetDatabase.FindAssets("ColliderEditorIconConcaveMesh t:texture2D");
			if(locs.Length > 0)
			{
				colliderIcons[5] = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(locs[0]));
			}
		}

		/// <summary>
		/// checks to see if the icons have moved / need to be found.
		/// </summary>
		public static void VerifyIcons()
		{
			bool needsUpdate = false;
			if (colliderIcons == null)
			{
				GetIcons();
			}
			else
			{
				foreach (Texture2D t2d in colliderIcons)
				{
					if (t2d == null)
					{
						needsUpdate = true;
					}
				}
				if (needsUpdate)
				{
					GetIcons();
				}
			}
		}

		private static GUIStyle highlightedStyle;

		private static GUIStyle HighLightedStyle
		{
			get
			{
				if (highlightedStyle == null)
				{
					highlightedStyle = new GUIStyle(GUI.skin.box);
					//Texture2D text = new Texture2D(1, 1);
					//text.SetPixel(0, 0, Color.red);
					//highlightedStyle.normal.background = text;
					highlightedStyle.normal.background = GUI.skin.button.onActive.background;

					highlightedStyle.fixedHeight = 0;
					highlightedStyle.padding = new RectOffset(4, 4, 1, 16);
				}
				return highlightedStyle;
			}
		}

		private static GUIStyle iconButtonLabelStyle;
		private static GUIStyle IconButtonLabelStyle
		{
			get
			{
				if (iconButtonLabelStyle == null)
				{
					iconButtonLabelStyle = new GUIStyle(GUI.skin.box);
					iconButtonLabelStyle.richText = true;
					iconButtonLabelStyle.fixedHeight = 16;
					//iconButtonLabelStyle.fixedWidth = 96;
					iconButtonLabelStyle.padding.bottom = 1;
					iconButtonLabelStyle.alignment = TextAnchor.LowerCenter;
				}
				return iconButtonLabelStyle;
			}
		}

		/// <summary>
		/// Helper method to create a button with a 32x32 Icon where the icons are found seperately and stored in an array.
		/// </summary>
		/// <param name="tooltip">tooltip to display when button is enabled</param>
		/// <param name="disabledToolTip">tooltip to display when disabled</param>
		/// <param name="iconNumber">icon number in texture2d array</param>
		/// <param name="isEnabled">is this icon enable?</param>
		/// <returns>true if enabled and the button is clicked, false otherwise</returns>
		public static bool DisableableIconButtonShortcutCreation(
			int iconNumber,
			bool isEnabled,
			bool isSelected,
			string label = null,
			string tooltip = null
		)
		{
			VerifyIcons();
			if (colliderIcons.Length - 1 < iconNumber || colliderIcons[iconNumber] == null) return false; // an icon was deleted.
			GUIStyle style = new GUIStyle(GUI.skin.button);

			style.padding = new RectOffset(4, 4, 1, 16);

			GUIStyle labelStyle = new GUIStyle(GUI.skin.box);
			labelStyle.richText = true;
			labelStyle.fixedHeight = 16;
			labelStyle.fixedWidth = 96;
			labelStyle.padding.bottom = 1;
			labelStyle.alignment = TextAnchor.LowerCenter;
			GUIContent content = new GUIContent(colliderIcons[iconNumber], tooltip);
			Rect r = GUILayoutUtility.GetRect(content, style);
			style.fixedHeight = 48;

			GUI.enabled = isEnabled;

			style.fixedHeight = 48;
			//content.tooltip = isEnabled ? tooltip : disabledToolTip;

			if(isSelected)
			{
				style = HighLightedStyle;
				//style.normal.background = GUI.skin.button.onActive.background;

			}

			bool button = GUI.Button(r, content, style);
			r.y += 32;

			// better dark mode colors, also light mode enabled/disabled button colors.
			string colorCode = isEnabled ? "black" : "#525252";
			if (EditorGUIUtility.isProSkin)
			{
				colorCode = isEnabled ? "#C4C4C4" : "#6C6C6C";
			}

			//string buttonLabel = string.IsNullOrEmpty(label) ? (iconNumber + 1).ToString() : label;
			string buttonLabel = (iconNumber + 1).ToString();

			GUIContent labelContent = new GUIContent("<color=" + colorCode + "><b>" + buttonLabel + "</b></color>", tooltip);
			GUI.Label(r, labelContent, IconButtonLabelStyle);
			GUI.enabled = true;
			return button;
		}
	}
}
