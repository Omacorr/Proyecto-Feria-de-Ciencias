using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Aviso de fotosensibilidad (epilepsia) al arrancar el juego. Pedido de Omar
/// (2026-09-24): el juego tiene parpadeos y destellos que ocupan todo el campo
/// visual del visor.
///
/// No se pone en ninguna escena: se crea solo, una vez por ejecucion, cuando la
/// primera escena que se carga es la de indice 0 del build (Menu Principal).
/// Asi sobrevive aunque el compañero regenere el Menu con su
/// MenuPrincipalGenerator, y no molesta al probar una Parte suelta en el Editor.
///
/// Es VR-safe por el mismo motivo que FaintOverlay: no usa un Canvas Screen
/// Space Overlay (URP no lo dibuja en los ojos con XR), sino un panel negro y
/// un TextMesh 3D pegados a la camara, con shaders que ignoran la profundidad.
/// Mientras esta en pantalla bloquea la mirada, para no tocar un boton del
/// menu sin querer.
///
/// La fuente se carga de Resources (Assets/Fonts/Resources/SpecialElite-Regular);
/// si no esta, usa la interna de Unity.
/// </summary>
public class PhotosensitivityWarning : MonoBehaviour
{
    private const string FontResource = "SpecialElite-Regular";
    private const float ShowSeconds = 8f;       // tiempo de lectura, sin contar el fundido
    private const float FadeSeconds = 1.2f;
    private const float TextDistance = 3f;      // metros adelante de la camara
    private const float LineHeight = 0.13f;     // alto de una linea a esa distancia (~2.5 grados)
    private const int BodyFontSize = 48;
    private const int TitleFontSize = 80;
    private const int PanelQueue = 4996;        // encima del reticle/FadeImage (4000), debajo de ScreenBlink/FaintOverlay
    private const int TextQueue = 4997;

    private const string Title = "ADVERTENCIA";
    private const string Body =
        "Este juego tiene luces que parpadean\n" +
        "y destellos que pueden provocar\n" +
        "convulsiones en personas con\n" +
        "epilepsia fotosensible.\n" +
        "\n" +
        "Si sentís mareo, molestias o ves\n" +
        "algo raro, sacate el visor\n" +
        "y dejá de jugar.\n" +
        "\n" +
        "Se recomienda jugar con auriculares.";

    private static bool s_shown;

    private Camera _camera;
    private Font _font;
    private Material _panelMat;
    private Material _textMat;
    private Mesh _panelMesh;
    private GameObject _panel;
    private GameObject _textGo;
    private TextMesh _text;
    private readonly List<GazeController> _blockedGaze = new List<GazeController>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        s_shown = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void ShowOnStartup()
    {
        if (s_shown)
        {
            return;
        }
        s_shown = true;
        if (SceneManager.GetActiveScene().buildIndex != 0)
        {
            return;
        }
        new GameObject("[Aviso de fotosensibilidad]").AddComponent<PhotosensitivityWarning>();
    }

    private IEnumerator Start()
    {
        // La camara del menu puede tardar un frame en existir.
        for (int i = 0; i < 30 && _camera == null; i++)
        {
            _camera = Camera.main;
            if (_camera == null)
            {
                yield return null;
            }
        }
        if (_camera == null)
        {
            Debug.LogWarning("[PhotosensitivityWarning] No encontre la camara: no se muestra el aviso.");
            Destroy(gameObject);
            yield break;
        }

        BlockGaze(true);
        BuildPanel();
        BuildText();
        Font.textureRebuilt += OnFontTextureRebuilt;
        Debug.Log("[PhotosensitivityWarning] Mostrando aviso de fotosensibilidad.");

        float t = 0f;
        while (t < ShowSeconds)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        t = 0f;
        while (t < FadeSeconds)
        {
            t += Time.unscaledDeltaTime;
            SetAlpha(1f - Mathf.Clamp01(t / FadeSeconds));
            yield return null;
        }

        BlockGaze(false);
        Destroy(gameObject);
    }

    private void BuildPanel()
    {
        _panelMat = OverlayUtil.CreateOverlayMaterial(null, "[PhotosensitivityWarning]", PanelQueue, false); // se crea por código, sin Inspector: UI/AlwaysOnTop entra al build por los materiales de la mira.
        if (_panelMat == null)
        {
            return;
        }
        _panelMat.color = Color.black;

        // Un quad pegado a la camara, lo bastante cerca y grande para tapar todo
        // el campo visual de cada ojo (mas alla del plano de corte cercano).
        float distance = Mathf.Max(0.5f, _camera.nearClipPlane * 3f);
        _panel = new GameObject("Panel");
        _panel.layer = OverlayUtil.IgnoreRaycastLayer;
        _panel.transform.SetParent(_camera.transform, false);
        _panel.transform.localPosition = new Vector3(0f, 0f, distance);
        _panel.transform.localScale = new Vector3(distance * 10f, distance * 10f, 1f);

        _panelMesh = BuildQuadMesh();
        _panel.AddComponent<MeshFilter>().sharedMesh = _panelMesh;
        MeshRenderer r = _panel.AddComponent<MeshRenderer>();
        r.sharedMaterial = _panelMat;
        OverlayUtil.ConfigureRenderer(r);
    }

    private void BuildText()
    {
        _font = Resources.Load<Font>(FontResource);
        if (_font == null || _font.material == null)
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
        if (_font == null || _font.material == null)
        {
            return;
        }

        // Una linea de TextMesh mide ~ fontSize * characterSize / 10.
        _text = OverlayUtil.CreateText("Texto", _camera.transform, _font, BodyFontSize,
            LineHeight * 10f / BodyFontSize, TextQueue, out _, out _textMat);
        _textGo = _text.gameObject;
        _textGo.transform.localPosition = new Vector3(0f, 0f, TextDistance);
        _text.text = "<size=" + TitleFontSize + "><color=#B3261E>" + Title + "</color></size>\n" +
                     "<i>fotosensibilidad</i>\n\n" + Body;
        _text.color = new Color(0.92f, 0.90f, 0.86f, 1f);
    }

    private void OnFontTextureRebuilt(Font f) => OverlayUtil.RefreshFontTexture(f, _font, _textMat);

    private void SetAlpha(float a)
    {
        if (_panelMat != null)
        {
            _panelMat.color = new Color(0f, 0f, 0f, a);
        }
        if (_text != null)
        {
            Color c = _text.color;
            c.a = a;
            _text.color = c;
        }
    }

    private void BlockGaze(bool block)
    {
        if (block)
        {
            OverlayUtil.DisableGaze(_blockedGaze);
        }
        else
        {
            OverlayUtil.RestoreGaze(_blockedGaze);
        }
    }

    private static Mesh BuildQuadMesh()
    {
        Mesh m = new Mesh { name = "AvisoPanel" };
        m.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f),
        };
        m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
        m.colors = new[] { Color.white, Color.white, Color.white, Color.white };
        // Las dos caras, por si el shader de respaldo recorta la de atras.
        m.triangles = new[] { 0, 2, 1, 2, 3, 1, 0, 1, 2, 2, 1, 3 };
        m.RecalculateBounds();
        return m;
    }

    private void OnDestroy()
    {
        Font.textureRebuilt -= OnFontTextureRebuilt;
        BlockGaze(false);
        if (_panelMat != null)
        {
            Destroy(_panelMat);
        }
        if (_textMat != null)
        {
            Destroy(_textMat);
        }
        if (_panelMesh != null)
        {
            Destroy(_panelMesh);
        }
        if (_panel != null)
        {
            Destroy(_panel);
        }
        if (_textGo != null)
        {
            Destroy(_textGo);
        }
    }
}
