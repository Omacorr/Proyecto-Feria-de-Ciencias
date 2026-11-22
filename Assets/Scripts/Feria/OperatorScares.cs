using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sustos, sonidos y textos que dispara el operador de la feria desde el PC
/// (comandos scare / tension / sfx / say en OperatorCommandServer). Funciona en
/// cualquier escena: si la escena tiene sus propias piezas (ExamineScareSounds,
/// Screamer, TensionAudio, VRHintText) las usa; si no, usa la biblioteca
/// Resources/OperatorSfxLibrary.
///
/// Se crea solo al arrancar (RuntimeInitializeOnLoadMethod) y sobrevive los
/// cambios de escena (DontDestroyOnLoad). Todos los metodos estaticos devuelven
/// "ok" o un motivo corto de error. Llamar desde el hilo principal.
///
/// Cooldown de 1,5 s (tiempo sin escala) entre sustos, para que el operador no
/// los apile. Ninguno hace flashes: el apagon es un corte unico con
/// TurnOff/TurnOn (sin parpadeo), por fotosensibilidad.
/// </summary>
public class OperatorScares : MonoBehaviour
{
    /// <summary>Nombres de los clips de la biblioteca (archivo sin extension).</summary>
    public static readonly string[] ClipNames =
    {
        "susurro_01", "susurro_02", "susurro_03", "susurro_04", "susurro_05",
        "respiracion_01", "pasos_concreto", "pasos_madera",
        "golpes_madera", "crujido_madera_01", "crujido_madera_02", "crujido_madera_03",
        "golpe_seco", "monstruo_rugido", "monstruo_respiracion", "latidos_despertar",
        "tv_estatica", "grito_screamer", "derrumbe_madera", "caida_viento", "zumbido_grave",
    };

    private const float CooldownSeconds = 1.5f;
    private const float TensionHoldSeconds = 8f;
    private const float BlackoutSeconds = 2.5f;
    private const float SfxVolume = 0.9f;
    private const int PoolSize = 4;

    private static OperatorScares s_instance;
    private static OperatorSfxLibrary s_library;
    private static bool s_libraryLoaded;

    private float _lastScareAt = float.NegativeInfinity; // tiempo sin escala
    private AudioSource[] _pool;   // 3D, rotativas
    private AudioSource _flat;     // 2D (sfx y latidos de respaldo)
    private int _poolIndex;
    private Screamer _ownScreamer;
    private Coroutine _tensionRoutine;
    private TensionAudio _tensionTarget;
    private float _tensionBefore;

    // ---------------- Creacion ----------------

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        s_instance = null;
        s_library = null;
        s_libraryLoaded = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        GetOrCreate();
    }

    private static OperatorScares GetOrCreate()
    {
        if (s_instance == null)
        {
            GameObject go = new GameObject("OperatorScares (feria)");
            DontDestroyOnLoad(go);
            s_instance = go.AddComponent<OperatorScares>();
        }
        return s_instance;
    }

    private void Awake()
    {
        if (s_instance != null && s_instance != this)
        {
            Destroy(gameObject);
            return;
        }
        s_instance = this;

        _pool = new AudioSource[PoolSize];
        for (int i = 0; i < PoolSize; i++)
        {
            _pool[i] = CreateSource("Operador (3D " + i + ")", true);
        }
        _flat = CreateSource("Operador (2D)", false);
    }

    private void OnDestroy()
    {
        if (s_instance == this)
        {
            s_instance = null;
        }
    }

    private AudioSource CreateSource(string objName, bool is3D)
    {
        GameObject go = new GameObject(objName);
        go.transform.SetParent(transform, false);
        AudioSource s = go.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.loop = false;
        s.spatialBlend = is3D ? 1f : 0f;
        s.dopplerLevel = 0f;
        s.rolloffMode = AudioRolloffMode.Linear;
        s.minDistance = 1f;
        s.maxDistance = 20f;
        return s;
    }

    // ---------------- API publica (la llama OperatorCommandServer) ----------------

    /// <summary>
    /// kind: screamer | whisper | steps | distant | knock | blackout | heartbeat.
    /// Devuelve "ok", "cooldown", "unknown-kind" o un motivo de error.
    /// </summary>
    public static string Scare(string kind)
    {
        return GetOrCreate().DoScare((kind ?? string.Empty).Trim().ToLowerInvariant());
    }

    /// <summary>Fija la tension (0..1) de TensionAudio de la escena.</summary>
    public static string Tension(float value)
    {
        return GetOrCreate().DoTension(value);
    }

    /// <summary>Reproduce un clip de la biblioteca por nombre, en 2D.</summary>
    public static string Sfx(string name)
    {
        return GetOrCreate().DoSfx(name);
    }

    /// <summary>Muestra un texto en VR con el VRHintText de la escena.</summary>
    public static string Say(string text)
    {
        return GetOrCreate().DoSay(text);
    }

    // ---------------- Implementacion ----------------

    private string DoScare(string kind)
    {
        switch (kind)
        {
            case "screamer":
            case "whisper":
            case "steps":
            case "distant":
            case "knock":
            case "blackout":
            case "heartbeat":
                break;
            default:
                return "unknown-kind";
        }

        if (Time.unscaledTime - _lastScareAt < CooldownSeconds)
        {
            return "cooldown";
        }

        string result;
        switch (kind)
        {
            case "screamer": result = DoScreamer(); break;
            case "whisper": result = DoWhisper(); break;
            case "steps": result = DoSteps(); break;
            case "distant": result = DoDistant(); break;
            case "knock": result = DoKnock(); break;
            case "blackout": result = DoBlackout(); break;
            default: result = DoHeartbeat(); break;
        }

        if (result == "ok")
        {
            _lastScareAt = Time.unscaledTime;
        }
        return result;
    }

    private string DoWhisper()
    {
        ExamineScareSounds ess = FindExamineSounds();
        if (ess != null && ess.HasWhisperClips)
        {
            ess.PlayWhisperNow();
            return "ok";
        }

        Transform cam = CameraTransform();
        if (cam == null) return "no-camera";
        AudioClip clip = RandomOf("susurro_01", "susurro_02", "susurro_03", "susurro_04", "susurro_05");
        if (clip == null) return "no-clips";

        // Pegado a un oido (izquierdo o derecho), 35 cm de la cabeza.
        AudioSource s = NextSource();
        float side = Random.value < 0.5f ? -1f : 1f;
        s.transform.SetParent(cam, false);
        s.transform.localPosition = new Vector3(side * 0.35f, 0f, -0.08f);
        PlayClip(s, clip, SfxVolume, Random.Range(0.92f, 1.05f));
        return "ok";
    }

    private string DoSteps()
    {
        ExamineScareSounds ess = FindExamineSounds();
        if (ess != null && ess.HasFootstepsClip)
        {
            ess.PlayFootstepsNow();
            return "ok";
        }

        Transform cam = CameraTransform();
        if (cam == null) return "no-camera";
        AudioClip clip = RandomOf("pasos_concreto", "pasos_madera");
        if (clip == null) return "no-clips";

        // Unos 5 m a espaldas, a la altura del piso.
        Vector3 pos = cam.position + BackDirection(cam) * 5f;
        pos.y = cam.position.y - 1.2f;
        PlayClip(PlaceWorld(NextSource(), pos), clip, SfxVolume, 0.85f);
        return "ok";
    }

    private string DoDistant()
    {
        ExamineScareSounds ess = FindExamineSounds();
        if (ess != null && ess.HasDistantClips)
        {
            ess.PlayDistantNow();
            return "ok";
        }

        Transform cam = CameraTransform();
        if (cam == null) return "no-camera";
        AudioClip clip = RandomOf("derrumbe_madera", "golpe_seco");
        if (clip == null) return "no-clips";

        // 8 m en una direccion al azar, alrededor del jugador.
        Vector3 dir = Quaternion.AngleAxis(Random.Range(0f, 360f), Vector3.up) * Vector3.forward;
        Vector3 pos = cam.position + dir * 8f;
        pos.y = cam.position.y + Random.Range(-1f, 0.5f);
        PlayClip(PlaceWorld(NextSource(), pos), clip, SfxVolume, Random.Range(0.85f, 1.1f));
        return "ok";
    }

    private string DoKnock()
    {
        Transform cam = CameraTransform();
        if (cam == null) return "no-camera";
        AudioClip clip = RandomOf("golpes_madera", "crujido_madera_01", "crujido_madera_02", "crujido_madera_03");
        if (clip == null) return "no-clips";

        // 4-6 m detras del jugador, con algo de angulo.
        Vector3 back = Quaternion.AngleAxis(Random.Range(-30f, 30f), Vector3.up) * BackDirection(cam);
        Vector3 pos = cam.position + back * Random.Range(4f, 6f);
        pos.y = cam.position.y - 0.5f;
        PlayClip(PlaceWorld(NextSource(), pos), clip, SfxVolume, Random.Range(0.9f, 1.05f));
        return "ok";
    }

    private string DoBlackout()
    {
        // Todas las luces menos las que cuelgan de la camara (la linterna).
        Transform cam = CameraTransform();
        var lights = new List<FlickeringLight>();
        foreach (FlickeringLight fl in FindObjectsByType<FlickeringLight>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (fl == null) continue;
            if (cam != null && fl.transform.IsChildOf(cam)) continue;
            fl.TurnOff();
            lights.Add(fl);
        }
        if (lights.Count == 0)
        {
            return "no-lights";
        }
        StartCoroutine(RestoreLights(lights));
        return "ok";
    }

    private IEnumerator RestoreLights(List<FlickeringLight> lights)
    {
        yield return new WaitForSecondsRealtime(BlackoutSeconds);
        foreach (FlickeringLight fl in lights)
        {
            if (fl != null)
            {
                fl.TurnOn();
            }
        }
    }

    private string DoHeartbeat()
    {
        TensionAudio ta = FindFirstObjectByType<TensionAudio>();
        if (ta != null && ta.isActiveAndEnabled)
        {
            // Si ya hay un latido en curso, se conserva el valor de antes (no el 1 del latido anterior).
            if (_tensionRoutine == null || _tensionTarget != ta)
            {
                _tensionBefore = ta.Tension;
                _tensionTarget = ta;
            }
            ta.SetTension(1f);
            if (_tensionRoutine != null)
            {
                StopCoroutine(_tensionRoutine);
            }
            _tensionRoutine = StartCoroutine(RestoreTension(ta, _tensionBefore));
            return "ok";
        }

        AudioClip clip = Clip("latidos_despertar");
        if (clip == null) return "no-clips";
        PlayClip(_flat, clip, SfxVolume, 1f);
        return "ok";
    }

    private IEnumerator RestoreTension(TensionAudio ta, float value)
    {
        yield return new WaitForSecondsRealtime(TensionHoldSeconds);
        if (ta != null)
        {
            ta.SetTension(value);
        }
        _tensionRoutine = null;
    }

    private string DoScreamer()
    {
        // Primero un Screamer de la escena (Parte 4 tiene uno).
        foreach (Screamer s in FindObjectsByType<Screamer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (s == null || !s.isActiveAndEnabled || s == _ownScreamer) continue;
            if (s.IsRunning) return "busy";
            if (s.TriggerForced()) return "ok";
        }

        // Si no hay, uno propio con el prefab de la biblioteca.
        Screamer own = GetOwnScreamer();
        if (own == null) return "no-monster";
        if (own.IsRunning) return "busy";
        return own.TriggerForced() ? "ok" : "no-monster";
    }

    private Screamer GetOwnScreamer()
    {
        if (_ownScreamer != null)
        {
            return _ownScreamer;
        }
        OperatorSfxLibrary lib = Library();
        if (lib == null || lib.monsterPrefab == null)
        {
            return null;
        }

        GameObject holder = new GameObject("Susto del operador");
        GameObject visual = Instantiate(lib.monsterPrefab, holder.transform);
        visual.name = "Monstruo (susto del operador)";
        visual.SetActive(false);
        Transform head = FindChildRecursive(visual.transform, "Head");

        AudioClip scream = lib.screamClip != null ? lib.screamClip : lib.Find("grito_screamer");
        _ownScreamer = holder.AddComponent<Screamer>();
        _ownScreamer.Setup(visual, head, scream);
        return _ownScreamer;
    }

    private string DoTension(float value)
    {
        TensionAudio ta = FindFirstObjectByType<TensionAudio>();
        if (ta == null || !ta.isActiveAndEnabled)
        {
            return "no tension";
        }
        // Un valor manual pisa un latido pendiente (que restauraria el anterior).
        if (_tensionRoutine != null)
        {
            StopCoroutine(_tensionRoutine);
            _tensionRoutine = null;
        }
        ta.SetTension(Mathf.Clamp01(value));
        return "ok";
    }

    private string DoSfx(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "bad-arg";
        }
        if (Library() == null)
        {
            return "no-library";
        }
        AudioClip clip = Clip(name.Trim());
        if (clip == null)
        {
            return "unknown clip";
        }
        PlayClip(_flat, clip, SfxVolume, 1f);
        return "ok";
    }

    private string DoSay(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "empty-text";
        }
        VRHintText hint = FindFirstObjectByType<VRHintText>();
        if (hint == null || !hint.isActiveAndEnabled)
        {
            return "no hint text";
        }
        hint.Show(text.Trim());
        return "ok";
    }

    // ---------------- Auxiliares ----------------

    private static OperatorSfxLibrary Library()
    {
        if (!s_libraryLoaded)
        {
            s_libraryLoaded = true;
            s_library = Resources.Load<OperatorSfxLibrary>("OperatorSfxLibrary");
            if (s_library == null)
            {
                Debug.LogWarning("[OperatorScares] No encuentro Resources/OperatorSfxLibrary: correr Tools > Feria de Ciencias > Operador: armar biblioteca de sonidos.");
            }
        }
        return s_library;
    }

    private static AudioClip Clip(string name)
    {
        OperatorSfxLibrary lib = Library();
        return lib != null ? lib.Find(name) : null;
    }

    private static AudioClip RandomOf(params string[] names)
    {
        var found = new List<AudioClip>();
        foreach (string n in names)
        {
            AudioClip c = Clip(n);
            if (c != null) found.Add(c);
        }
        if (found.Count == 0) return null;
        return found[Random.Range(0, found.Count)];
    }

    private static ExamineScareSounds FindExamineSounds()
    {
        ExamineScareSounds ess = FindFirstObjectByType<ExamineScareSounds>();
        return ess != null && ess.isActiveAndEnabled ? ess : null;
    }

    private static Transform CameraTransform()
    {
        Camera cam = Camera.main;
        return cam != null ? cam.transform : null;
    }

    // Direccion horizontal "hacia atras" de la camara (si mira derecho al piso, usa -Z).
    private static Vector3 BackDirection(Transform cam)
    {
        Vector3 back = -Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
        if (back.sqrMagnitude < 0.01f)
        {
            back = -Vector3.forward;
        }
        return back;
    }

    private static Transform FindChildRecursive(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (string.Equals(t.name, name, System.StringComparison.OrdinalIgnoreCase))
            {
                return t;
            }
        }
        return null;
    }

    // Siguiente fuente del pool (rotativa).
    private AudioSource NextSource()
    {
        AudioSource s = _pool[_poolIndex];
        _poolIndex = (_poolIndex + 1) % _pool.Length;
        s.Stop();
        return s;
    }

    private AudioSource PlaceWorld(AudioSource s, Vector3 worldPos)
    {
        s.transform.SetParent(transform, true);
        s.transform.position = worldPos;
        return s;
    }

    private static void PlayClip(AudioSource s, AudioClip clip, float volume, float pitch)
    {
        s.Stop();
        s.clip = clip;
        s.volume = volume;
        s.pitch = pitch;
        s.Play();
    }
}
