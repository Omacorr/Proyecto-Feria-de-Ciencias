using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Screen-center reticle with a radial fill ring that shows gaze progress.
/// Built at runtime so no manual UI setup is required.
/// </summary>
[DisallowMultipleComponent]
public class GazeReticleUI : MonoBehaviour
{
    public enum ReticleMode
    {
        Idle,
        Interactable,
        Teleport
    }

    [SerializeField]
    Color idleColor = new Color(1f, 1f, 1f, 0.85f);

    [SerializeField]
    Color interactableColor = new Color(0.2f, 0.95f, 0.55f, 0.95f);

    [SerializeField]
    Color teleportColor = new Color(0.35f, 0.65f, 1f, 0.95f);

    [SerializeField]
    float dotSize = 10f;

    [SerializeField]
    float ringSize = 56f;

    [SerializeField]
    float ringThickness = 4f;

    Image _centerDot;
    Image _ringBackground;
    Image _ringFill;
    Canvas _canvas;

    public ReticleMode CurrentMode { get; private set; } = ReticleMode.Idle;

    void Awake()
    {
        BuildReticle();
    }

    void BuildReticle()
    {
        var canvasObject = new GameObject("GazeReticleCanvas");
        canvasObject.transform.SetParent(transform, false);

        _canvas = canvasObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 100;

        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        canvasObject.AddComponent<GraphicRaycaster>();

        var sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");

        _centerDot = CreateImage(canvasObject.transform, "CenterDot", sprite, dotSize, idleColor);

        _ringBackground = CreateRing(canvasObject.transform, "RingBackground", sprite, ringSize, ringThickness,
            new Color(1f, 1f, 1f, 0.2f), false);

        _ringFill = CreateRing(canvasObject.transform, "RingFill", sprite, ringSize, ringThickness, interactableColor, true);
        _ringFill.fillAmount = 0f;
        _ringFill.enabled = false;
    }

    static Image CreateImage(Transform parent, string name, Sprite sprite, float size, Color color)
    {
        var imageObject = new GameObject(name, typeof(RectTransform));
        imageObject.transform.SetParent(parent, false);

        var rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(size, size);
        rect.anchoredPosition = Vector2.zero;

        var image = imageObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    static Image CreateRing(Transform parent, string name, Sprite sprite, float size, float thickness, Color color,
        bool filled)
    {
        var image = CreateImage(parent, name, sprite, size, color);
        image.type = filled ? Image.Type.Filled : Image.Type.Simple;

        if (filled)
        {
            image.fillMethod = Image.FillMethod.Radial360;
            image.fillOrigin = (int)Image.Origin360.Top;
            image.fillClockwise = true;
        }

        return image;
    }

    public void SetMode(ReticleMode mode)
    {
        CurrentMode = mode;

        switch (mode)
        {
            case ReticleMode.Interactable:
                _centerDot.color = interactableColor;
                _ringFill.color = interactableColor;
                _ringFill.enabled = true;
                break;

            case ReticleMode.Teleport:
                _centerDot.color = teleportColor;
                _ringFill.color = teleportColor;
                _ringFill.enabled = true;
                break;

            default:
                _centerDot.color = idleColor;
                _ringFill.fillAmount = 0f;
                _ringFill.enabled = false;
                break;
        }
    }

    public void SetProgress(float normalizedProgress)
    {
        if (_ringFill == null)
        {
            return;
        }

        _ringFill.fillAmount = Mathf.Clamp01(normalizedProgress);
    }
}
