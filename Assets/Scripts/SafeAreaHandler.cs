using UnityEngine;

public class SafeAreaHandler : MonoBehaviour
{
    private RectTransform _rect;
    private Rect _lastSafeArea;
    private ScreenOrientation _lastOrientation;

    private void Awake()
    {
        _rect = GetComponent<RectTransform>();
        Apply();
    }

    private void Update()
    {
        // Re-apply if screen rotates or safe area changes
        if (Screen.safeArea != _lastSafeArea ||
            Screen.orientation != _lastOrientation)
        {
            Apply();
        }
    }

    private void Apply()
    {
        Rect safe = Screen.safeArea;
        _lastSafeArea = safe;
        _lastOrientation = Screen.orientation;

        Vector2 screenSize = new Vector2(Screen.width, Screen.height);

        // Convert safe area to anchor coordinates (0–1 range)
        Vector2 anchorMin = safe.position / screenSize;
        Vector2 anchorMax = (safe.position + safe.size) / screenSize;

        _rect.anchorMin = anchorMin;
        _rect.anchorMax = anchorMax;
        _rect.offsetMin = Vector2.zero;
        _rect.offsetMax = Vector2.zero;
    }
}