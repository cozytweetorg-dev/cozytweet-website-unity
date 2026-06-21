using UnityEditor;
using UnityEngine;

/// <summary>
/// Custom Inspector for CozyTweetWebsite.
/// Place this file inside any folder named "Editor" in your Unity project.
/// e.g. Assets/Scripts/Editor/CozyTweetWebsiteEditor.cs
///
/// The debug panel only appears in the Editor — it is automatically
/// stripped from all builds so it never ships to players.
/// </summary>

[CustomEditor(typeof(CozyTweetWebsite))]
public class CozyTweetWebsiteEditor : Editor
{
    // ─────────────────────────────────────────────
    //  STATE
    // ─────────────────────────────────────────────

    private string _removeEmailInput  = "";
    private bool   _debugFoldout      = true;
    private bool   _confirmClearAll   = false;

    // ─────────────────────────────────────────────
    //  STYLES  (lazy-initialised)
    // ─────────────────────────────────────────────

    private GUIStyle _boxStyle;
    private GUIStyle _headerStyle;
    private GUIStyle _dangerButton;
    private GUIStyle _successButton;
    private GUIStyle _warningButton;

    private void InitStyles()
    {
        if (_headerStyle != null) return;

        _headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize  = 12,
            alignment = TextAnchor.MiddleLeft,
        };

        _boxStyle = new GUIStyle(EditorStyles.helpBox)
        {
            padding = new RectOffset(12, 12, 10, 10),
        };

        // Pink-ish remove button
        _dangerButton = new GUIStyle(GUI.skin.button)
        {
            fontStyle = FontStyle.Bold,
            normal    = { textColor = Color.white, background = MakeColorTex(new Color(0.85f, 0.30f, 0.45f)) },
            hover     = { textColor = Color.white, background = MakeColorTex(new Color(0.75f, 0.22f, 0.38f)) },
            active    = { textColor = Color.white, background = MakeColorTex(new Color(0.65f, 0.15f, 0.30f)) },
        };

        // Lavender confirm button
        _successButton = new GUIStyle(GUI.skin.button)
        {
            fontStyle = FontStyle.Bold,
            normal    = { textColor = Color.white, background = MakeColorTex(new Color(0.55f, 0.42f, 0.75f)) },
            hover     = { textColor = Color.white, background = MakeColorTex(new Color(0.46f, 0.34f, 0.65f)) },
            active    = { textColor = Color.white, background = MakeColorTex(new Color(0.38f, 0.27f, 0.55f)) },
        };

        // Soft amber warning button
        _warningButton = new GUIStyle(GUI.skin.button)
        {
            fontStyle = FontStyle.Bold,
            normal    = { textColor = Color.white, background = MakeColorTex(new Color(0.85f, 0.58f, 0.15f)) },
            hover     = { textColor = Color.white, background = MakeColorTex(new Color(0.75f, 0.48f, 0.08f)) },
            active    = { textColor = Color.white, background = MakeColorTex(new Color(0.65f, 0.40f, 0.04f)) },
        };
    }

    // ─────────────────────────────────────────────
    //  DRAW INSPECTOR
    // ─────────────────────────────────────────────

    public override void OnInspectorGUI()
    {
        InitStyles();

        // Draw all the default serialized fields (Formspree, EmailJS, UI refs, fade settings)
        DrawDefaultInspector();

        EditorGUILayout.Space(14);

        // ── Debug panel (Editor-only) ─────────────
        _debugFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(_debugFoldout, "🛠  Registry Debug Tools");

        if (_debugFoldout)
        {
            EditorGUILayout.BeginVertical(_boxStyle);

            EditorGUILayout.LabelField("Only visible in Editor — stripped from builds.", EditorStyles.miniLabel);
            EditorGUILayout.Space(6);

            // ── Print all ──────────────────────────
            DrawSectionHeader("📋  Inspect Registry");
            EditorGUILayout.Space(2);

            if (GUILayout.Button("Print All Registered Emails to Console", _successButton, GUILayout.Height(32)))
            {
                if (Application.isPlaying)
                    ((CozyTweetWebsite)target).PrintRegisteredEmails();
                else
                    Debug.LogWarning("[CozyTweet Editor] Enter Play Mode to use debug tools.");
            }

            EditorGUILayout.Space(10);

            // ── Remove single email ────────────────
            DrawSectionHeader("🗑  Remove Single Email");
            EditorGUILayout.Space(2);

            EditorGUILayout.BeginHorizontal();
            _removeEmailInput = EditorGUILayout.TextField(_removeEmailInput, GUILayout.Height(22));
            GUI.enabled = !string.IsNullOrWhiteSpace(_removeEmailInput);

            if (GUILayout.Button("Remove", _dangerButton, GUILayout.Width(80), GUILayout.Height(22)))
            {
                if (Application.isPlaying)
                {
                    ((CozyTweetWebsite)target).RemoveRegisteredEmail(_removeEmailInput.Trim());
                    _removeEmailInput = "";
                    GUI.FocusControl(null);
                }
                else
                {
                    Debug.LogWarning("[CozyTweet Editor] Enter Play Mode to use debug tools.");
                }
            }

            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox(
                "Type an email address above and press Remove to delete it from the local registry.",
                MessageType.None
            );

            EditorGUILayout.Space(10);

            // ── Clear all ──────────────────────────
            DrawSectionHeader("⚠️  Clear Entire Registry");
            EditorGUILayout.Space(2);

            if (!_confirmClearAll)
            {
                if (GUILayout.Button("Clear ALL Registered Emails", _warningButton, GUILayout.Height(32)))
                    _confirmClearAll = true;
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "This will permanently delete all locally stored emails. Are you sure?",
                    MessageType.Warning
                );

                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button("Yes, Clear All", _dangerButton, GUILayout.Height(28)))
                {
                    if (Application.isPlaying)
                    {
                        ((CozyTweetWebsite)target).ClearAllRegisteredEmails();
                        _confirmClearAll = false;
                    }
                    else
                    {
                        Debug.LogWarning("[CozyTweet Editor] Enter Play Mode to use debug tools.");
                        _confirmClearAll = false;
                    }
                }

                if (GUILayout.Button("Cancel", GUILayout.Height(28)))
                    _confirmClearAll = false;

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    // ─────────────────────────────────────────────
    //  HELPERS
    // ─────────────────────────────────────────────

    private static void DrawSectionHeader(string label)
    {
        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        Rect r = GUILayoutUtility.GetLastRect();
        r.y    += EditorGUIUtility.singleLineHeight - 2;
        r.height = 1;
        EditorGUI.DrawRect(r, new Color(0.5f, 0.5f, 0.5f, 0.25f));
        EditorGUILayout.Space(2);
    }

    /// <summary>Creates a 1x1 solid colour Texture2D for button backgrounds.</summary>
    private static Texture2D MakeColorTex(Color color)
    {
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, color);
        tex.Apply();
        return tex;
    }
}
