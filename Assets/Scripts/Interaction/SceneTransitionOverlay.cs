using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Fundido a negro con texto de nivel + carga de escena, en un objeto que
/// SOBREVIVE el cambio de escena (DontDestroyOnLoad): asi el fundido de salida
/// se completa ya adentro de la escena nueva y no queda un corte feo.
///
/// Se dispara con SceneTransitionOverlay.Play(...). No hace falta ponerlo en
/// ninguna escena.
/// </summary>
public class SceneTransitionOverlay : MonoBehaviour
{
    public static void Play(string sceneName, string bigText, string subText,
                            float fadeDuration = 1.2f, float holdSeconds = 2.5f, Color? color = null)
    {
        GameObject go = new GameObject("SceneTransitionOverlay");
        DontDestroyOnLoad(go);
        SceneTransitionOverlay o = go.AddComponent<SceneTransitionOverlay>();
        o._scene = sceneName;
        o._big = bigText;
        o._sub = subText;
        o._fade = Mathf.Max(0.05f, fadeDuration);
        o._hold = Mathf.Max(0f, holdSeconds);
        o._color = color ?? Color.black;
        o.StartCoroutine(o.Run());
    }

    private string _scene;
    private string _big;
    private string _sub;
    private float _fade;
    private float _hold;
    private Color _color;

    private IEnumerator Run()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32760;
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        Image img = NewChild<Image>("Negro");
        Stretch(img.rectTransform);
        img.raycastTarget = false;
        img.color = new Color(_color.r, _color.g, _color.b, 0f);

        TextMeshProUGUI txt = NewChild<TextMeshProUGUI>("Texto");
        RectTransform trt = txt.rectTransform;
        trt.anchorMin = new Vector2(0.5f, 0.5f);
        trt.anchorMax = new Vector2(0.5f, 0.5f);
        trt.sizeDelta = new Vector2(1800f, 700f);
        trt.anchoredPosition = Vector2.zero;
        txt.alignment = TextAlignmentOptions.Center;
        txt.richText = true;
        txt.text = "<b>" + _big + "</b>\n<size=52%>" + _sub + "</size>";
        txt.fontSize = 150f;
        txt.color = Color.white;
        txt.alpha = 0f;
        TMP_FontAsset font = TMP_Settings.defaultFontAsset;
        if (font != null) txt.font = font;

        // 1. fundido a negro
        yield return Fade(img, txt, 0f, 1f);

        // 2. cargar la escena (sin activarla todavia)
        AsyncOperation op = SceneManager.LoadSceneAsync(_scene);
        if (op == null)
        {
            Debug.LogError("[SceneTransitionOverlay] No pude cargar '" + _scene + "'. Esta en File > Build Settings?");
            yield return Fade(img, txt, 1f, 0f);
            Destroy(gameObject);
            yield break;
        }
        op.allowSceneActivation = false;

        float t = 0f;
        while (t < _hold || op.progress < 0.9f)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        op.allowSceneActivation = true;

        // 3. ya en la escena nueva: fundido de salida y limpieza
        yield return null;
        yield return new WaitForSecondsRealtime(0.2f);
        yield return Fade(img, txt, 1f, 0f);
        Destroy(gameObject);
    }

    private IEnumerator Fade(Image img, TMP_Text txt, float from, float to)
    {
        float e = 0f;
        while (e < _fade)
        {
            e += Time.unscaledDeltaTime;
            float a = Mathf.Lerp(from, to, Mathf.Clamp01(e / _fade));
            if (img != null) img.color = new Color(_color.r, _color.g, _color.b, a);
            if (txt != null) txt.alpha = a;
            yield return null;
        }
        if (img != null) img.color = new Color(_color.r, _color.g, _color.b, to);
        if (txt != null) txt.alpha = to;
    }

    private T NewChild<T>(string childName) where T : Component
    {
        GameObject c = new GameObject(childName, typeof(RectTransform));
        c.transform.SetParent(transform, false);
        return c.AddComponent<T>();
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
