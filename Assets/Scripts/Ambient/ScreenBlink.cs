using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Parpadeo de los OJOS del protagonista (no de las luces), VR-safe para
/// Cardboard: dos parpados que cierran desde arriba y desde abajo con borde
/// curvo y suave (shader Custom/EyelidOverlay sobre una esfera chica que sigue
/// a Camera.main; mismo truco que FaintOverlay, porque un Canvas Screen Space
/// Overlay no se ve en estereo - gotcha #7).
///
/// Usos segun el guion "Recuerdos Rotos":
///  - Etapa 5 (final): "parpadeo rapido" antes del pitido del monitor de
///    hospital -> RapidBlinks() desde un UnityEvent (con "Close After Rapid"
///    para terminar en negro, y On Sequence Finished para encadenar el
///    pitido / el cartel de FIN).
///  - Etapa 1: arrancar a oscuras y "despertar" -> Start Closed + Auto Open
///    Delay (o OpenEyes() desde un evento cuando terminen los latidos).
///  - Sustos puntuales dentro de una escena -> Blink().
/// La transicion ENTRE escenas (desmayo) NO es esto: es FaintTransition.
///
/// Todos los metodos publicos se pueden llamar desde cualquier UnityEvent
/// (sin parametros, o con un int/bool fijo desde el Inspector).
/// Mientras los ojos estan abiertos el renderer se apaga: no cuesta nada.
/// </summary>
public class ScreenBlink : MonoBehaviour
{
    [Header("Render")]
    [Tooltip("Arrastrar Assets/Shaders/Eyelid_Overlay.shader. Asignarlo SIEMPRE: si nada lo referencia, Unity no lo incluye en el APK y en el celular no habria parpadeo.")]
    [SerializeField] private Shader _eyelidShader;
    [Tooltip("Color de los parpados (negro). Blanco sirve para un destello de luz.")]
    [SerializeField] private Color _color = Color.black;
    [Range(0.005f, 0.5f)]
    [Tooltip("Suavidad del borde de los parpados.")]
    [SerializeField] private float _softness = 0.12f;
    [Range(0f, 1f)]
    [Tooltip("Curvatura del borde (0 = recto, como una persiana).")]
    [SerializeField] private float _curve = 0.35f;

    [Header("Parpadeo (Blink)")]
    [SerializeField] private float _closeTime = 0.08f;
    [SerializeField] private float _closedHold = 0.06f;
    [SerializeField] private float _openTime = 0.14f;

    [Header("Parpadeo rapido (RapidBlinks)")]
    [SerializeField] private int _rapidCount = 5;
    [Tooltip("Pausa con los ojos abiertos entre parpadeos. Con los valores por defecto quedan ~2.5 parpadeos por segundo, por debajo del limite de 3 destellos/seg que se recomienda por fotosensibilidad (el parpadeo ocupa TODO el campo visual en el visor). No bajar de ~0.12.")]
    [SerializeField] private float _rapidPause = 0.15f;
    [Tooltip("Al terminar los parpadeos rapidos, cerrar los ojos y quedarse en negro (final del juego).")]
    [SerializeField] private bool _closeAfterRapid = false;

    [Header("Abrir / cerrar lento")]
    [SerializeField] private float _slowDuration = 1.2f;
    [Tooltip("La escena arranca con los ojos cerrados (todo del color).")]
    [SerializeField] private bool _startClosed = false;
    [Tooltip("Si arranca cerrado: segundos hasta abrir solo (con un parpadeo de despertar). Negativo = no abre solo; llamar OpenEyes() desde un evento.")]
    [SerializeField] private float _autoOpenDelay = 2f;

    [Header("Eventos")]
    [Tooltip("Se dispara al terminar cualquier secuencia (Blink, RapidBlinks, CloseEyes, OpenEyes, despertar). Para encadenar sonido, cartel de FIN, etc.")]
    [SerializeField] private UnityEvent _onSequenceFinished;

    private static readonly int ClosedId = Shader.PropertyToID("_Closed");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int SoftnessId = Shader.PropertyToID("_Softness");
    private static readonly int CurveId = Shader.PropertyToID("_Curve");

    private Transform _sphere;
    private MeshRenderer _renderer;
    private Material _mat;
    private float _closed;
    private Coroutine _running;

    /// <summary>0 = abiertos, 1 = cerrados.</summary>
    public float Closedness => _closed;

    // ---------------- API para UnityEvents ----------------

    /// <summary>Un parpadeo (cierra, sostiene, abre).</summary>
    public void Blink()
    {
        var steps = new List<Step>();
        AddBlink(steps, _closeTime, _closedHold, _openTime, 0f);
        Play(steps);
    }

    /// <summary>"Rapid Count" parpadeos rapidos seguidos.</summary>
    public void RapidBlinks()
    {
        RapidBlinksCount(_rapidCount);
    }

    /// <summary>Parpadeos rapidos con cantidad elegida en el UnityEvent.</summary>
    public void RapidBlinksCount(int count)
    {
        count = Mathf.Max(1, count);
        var steps = new List<Step>();
        for (int i = 0; i < count; i++)
        {
            AddBlink(steps, _closeTime * 0.8f, _closedHold, _openTime * 0.8f, i < count - 1 ? _rapidPause : 0f);
        }
        if (_closeAfterRapid)
        {
            steps.Add(new Step(1f, _closeTime, 0f));
        }
        Play(steps);
    }

    /// <summary>Cierra los ojos despacio y se queda en negro.</summary>
    public void CloseEyes()
    {
        Play(new List<Step> { new Step(1f, _slowDuration, 0f) });
    }

    /// <summary>Abre los ojos despacio.</summary>
    public void OpenEyes()
    {
        Play(new List<Step> { new Step(0f, _slowDuration, 0f) });
    }

    /// <summary>Corte seco: true = negro instantaneo, false = ojos abiertos instantaneo.</summary>
    public void SetEyesClosed(bool closed)
    {
        Stop();
        SetClosed(closed ? 1f : 0f);
    }

    // ---------------- Ciclo de vida ----------------

    private void Awake()
    {
        Build();
        SetClosed(_startClosed ? 1f : 0f);
    }

    private void Start()
    {
        if (_startClosed && _autoOpenDelay >= 0f)
        {
            // Despertar: espera cerrado, abre a medias, vuelve a cerrar y abre
            // del todo (igual que el despertar de FaintOverlay).
            Play(new List<Step>
            {
                new Step(1f, 0f, _autoOpenDelay),
                new Step(0.55f, 0.35f, 0f),
                new Step(1f, 0.15f, 0.25f),
                new Step(0f, _slowDuration, 0f),
            });
        }
    }

    private void OnEnable()
    {
        Application.onBeforeRender += FollowCamera;
        if (_renderer != null)
        {
            _renderer.enabled = _closed > 0.001f;
        }
    }

    private void OnDisable()
    {
        Application.onBeforeRender -= FollowCamera;
        if (_renderer != null)
        {
            _renderer.enabled = false;
        }
    }

    private void OnDestroy()
    {
        if (_sphere != null)
        {
            Destroy(_sphere.gameObject);
        }
        if (_mat != null)
        {
            Destroy(_mat);
        }
    }

    private void LateUpdate()
    {
        FollowCamera();
    }

    // ---------------- Armado ----------------

    private void Build()
    {
        Shader sh = _eyelidShader != null ? _eyelidShader : Shader.Find("Custom/EyelidOverlay");
        if (sh == null)
        {
            Debug.LogWarning("[ScreenBlink] Falta asignar Eyelid Shader (Assets/Shaders/Eyelid_Overlay.shader): no hay parpadeo.");
            return;
        }

        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "ScreenBlink (parpados)";
        // Sin collider: si no, el Raycast del GazeController pegaria contra la esfera.
        DestroyImmediate(sphere.GetComponent<Collider>());
        sphere.layer = 2; // Ignore Raycast
        _sphere = sphere.transform; // en la raiz: la escala/rotacion de este objeto no la afecta

        _mat = new Material(sh);
        _mat.SetColor(ColorId, _color);
        _mat.SetFloat(SoftnessId, _softness);
        _mat.SetFloat(CurveId, _curve);

        _renderer = sphere.GetComponent<MeshRenderer>();
        _renderer.sharedMaterial = _mat;
        _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _renderer.receiveShadows = false;
        _renderer.enabled = false;
    }

    // Se llama en LateUpdate Y justo antes de renderizar, porque el
    // TrackedPoseDriver/Cardboard puede mover la camara despues del LateUpdate.
    private void FollowCamera()
    {
        if (_sphere == null)
        {
            return;
        }
        Camera cam = Camera.main;
        if (cam == null)
        {
            return;
        }
        _sphere.position = cam.transform.position;
        float d = Mathf.Max(0.5f, cam.nearClipPlane * 4f);
        _sphere.localScale = new Vector3(d, d, d);
    }

    private void SetClosed(float value)
    {
        _closed = Mathf.Clamp01(value);
        if (_mat != null)
        {
            _mat.SetFloat(ClosedId, _closed);
        }
        if (_renderer != null)
        {
            _renderer.enabled = isActiveAndEnabled && _closed > 0.001f;
        }
    }

    // ---------------- Secuencias ----------------

    // Un paso = ir hasta "To" (0 abierto .. 1 cerrado) en "Duration" segundos
    // y despues quedarse "Hold" segundos. Todo corre en UNA sola corrutina
    // (sin corrutinas anidadas), asi que StopCoroutine corta la secuencia
    // entera sin dejar nada peleando por el valor.
    private readonly struct Step
    {
        public readonly float To;
        public readonly float Duration;
        public readonly float Hold;

        public Step(float to, float duration, float hold)
        {
            To = to;
            Duration = duration;
            Hold = hold;
        }
    }

    private static void AddBlink(List<Step> steps, float close, float hold, float open, float pauseAfter)
    {
        steps.Add(new Step(1f, close, hold));
        steps.Add(new Step(0f, open, pauseAfter));
    }

    private void Play(List<Step> steps)
    {
        Stop();
        if (!isActiveAndEnabled)
        {
            return;
        }
        _running = StartCoroutine(Run(steps));
    }

    private void Stop()
    {
        if (_running != null)
        {
            StopCoroutine(_running);
            _running = null;
        }
    }

    private IEnumerator Run(List<Step> steps)
    {
        foreach (Step s in steps)
        {
            float from = _closed;
            float duration = Mathf.Max(0f, s.Duration);
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / duration);
                k = k * k * (3f - 2f * k); // suave al principio y al final
                SetClosed(Mathf.Lerp(from, s.To, k));
                yield return null;
            }
            SetClosed(s.To);
            if (s.Hold > 0f)
            {
                yield return new WaitForSecondsRealtime(s.Hold);
            }
        }
        _running = null;
        _onSequenceFinished?.Invoke();
    }
}
