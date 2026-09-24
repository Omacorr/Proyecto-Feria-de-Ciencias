using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// El "desmayo" de la historia (Recuerdos Rotos): la transicion estandar
/// entre etapas. Se pone UNO por escena (en un objeto propio, por ejemplo
/// "Transiciones/Desmayo -> Parte N", no colgado de un objeto que se apague) y
/// se dispara llamando Trigger() desde cualquier UnityEvent (CodeLock.On
/// Unlocked, TeleportPoint.On Player Arrived, etc.) o desde otro script
/// (AbyssFall, WallCollapse). TriggerToScene(nombre) hace lo mismo pero con
/// otro destino (UnityEvent con parametro string).
///
/// Secuencia:
///  1. Bloquea la mirada (nada mas se puede seleccionar) y dispara On Collapse Start.
///  2. Las luces asignadas parpadean violento (FlickeringLight.Burst) y, si hay
///     clip, suena un susurro 3D pegado a un oido.
///  3. Espera Pre Delay (para dejar ver la puerta abriendose, la caida, etc.).
///  4. Golpe seco + fundido rapido al color, carga de la escena siguiente, y
///     "abre los ojos" ya en la escena nueva (ver FaintOverlay).
///
/// Todo lo que pasa despues del disparo corre en FaintOverlay (que sobrevive
/// el cambio de escena), no en este componente: por eso Trigger() funciona
/// aunque este objeto se apague justo despues (por ejemplo, un SetActive(false)
/// en el mismo evento) o aunque ya este apagado.
///
/// Si la escena destino no esta agregada y tildada en Build Settings, Trigger()
/// NO hace nada (loguea un error): mejor que dejar al jugador en negro para siempre.
/// </summary>
public class FaintTransition : MonoBehaviour
{
    [Header("Destino")]
    [Tooltip("Nombre EXACTO de la escena siguiente (tiene que estar agregada y tildada en File > Build Settings).")]
    [SerializeField] private string _nextSceneName = "Parte 2 - Cumpleaños";

    [Header("Antes del colapso")]
    [Tooltip("Segundos entre Trigger() y el corte a negro (se ve la puerta abrirse, parpadean las luces, suena el susurro).")]
    [SerializeField] private float _preDelay = 2f;
    [Tooltip("Luces que parpadean violento durante el Pre Delay. Opcional.")]
    [SerializeField] private FlickeringLight[] _flickerLights;
    [Tooltip("Clip de susurro. Suena en 3D pegado a un oido del jugador. Opcional.")]
    [SerializeField] private AudioClip _whisperClip;
    [Tooltip("Oido del susurro: -1 izquierdo, 1 derecho.")]
    [SerializeField] private float _whisperSide = -1f;
    [Tooltip("Segundos despues de Trigger() en que arranca el susurro.")]
    [SerializeField] private float _whisperDelay = 0.3f;
    [Tooltip("Se dispara apenas arranca el colapso (antes del Pre Delay). Para sumar cosas extra desde el Inspector.")]
    [SerializeField] private UnityEvent _onCollapseStart;

    [Header("Desmayo")]
    [Tooltip("Color del desmayo. Negro por defecto; Parte 4 usa blanco-amarillo (la luz que te absorbe).")]
    [SerializeField] private Color _color = Color.black;
    [Tooltip("Duracion del fundido al color. Corto = golpe seco.")]
    [SerializeField] private float _fadeOutDuration = 0.12f;
    [Tooltip("Golpe seco. Suena (2D) justo al empezar el fundido al color: con Fade Out Duration corto (0.12) coincide con el corte a negro. Sigue sonando durante la carga. Opcional.")]
    [SerializeField] private AudioClip _impactClip;
    [Tooltip("Segundos minimos con la pantalla tapada antes de despertar.")]
    [SerializeField] private float _holdSeconds = 2f;

    [Header("Despertar (en la escena siguiente)")]
    [Tooltip("Duracion del fundido de vuelta al abrir los ojos.")]
    [SerializeField] private float _wakeDuration = 0.8f;
    [Tooltip("Parpadeo al abrir los ojos (abre a medias, cierra, abre).")]
    [SerializeField] private bool _blinkOnWake = true;
    [Tooltip("Sonido al despertar (respiracion agitada, latido). Opcional.")]
    [SerializeField] private AudioClip _wakeClip;

    [Header("Render")]
    [Tooltip("Arrastrar Assets/Materials/Mat_ReticleAlwaysOnTop. Hace que el desmayo tape todo, incluso geometria pegada a la camara, y GARANTIZA que su shader entre al build de Android (si queda vacio se busca con Shader.Find, que en el build puede fallar).")]
    [SerializeField] private Material _overlayMaterial;

    [Header("Texto de nivel (opcional - decision abierta #6)")]
    [Tooltip("Apagado = corte a negro seco, sin texto (como describe el guion). Prendido = muestra el titulo mientras la pantalla esta tapada.")]
    [SerializeField] private bool _showLevelText;
    [Tooltip("Texto grande, por ejemplo PARTE 2.")]
    [SerializeField] private string _levelTitle = "";
    [Tooltip("Texto chico debajo, por ejemplo CUMPLEAÑOS.")]
    [SerializeField] private string _levelSubtitle = "";

    private bool _triggered;

    public string NextSceneName => _nextSceneName;

    /// <summary>true si este desmayo ya se disparo (se dispara una sola vez por escena).</summary>
    public bool HasTriggered => _triggered;

    private void Start()
    {
        // Avisos tempranos, al arrancar la escena, en vez de enterarse recien al
        // resolver el puzzle.
        if (!CanLoad(_nextSceneName))
        {
            Debug.LogWarning("[FaintTransition] " + name + ": la escena '" + _nextSceneName + "' no esta agregada/tildada en File > Build Settings (o el nombre no coincide exacto). El desmayo no va a poder dispararse.");
        }
        if (_overlayMaterial == null)
        {
            Debug.LogWarning("[FaintTransition] " + name + ": Overlay Material vacio. Asignar Assets/Materials/Mat_ReticleAlwaysOnTop (en el build de Android el shader puede no encontrarse).");
        }
    }

    /// <summary>Punto de entrada para UnityEvent: desmayo hacia Next Scene Name.</summary>
    [ContextMenu("Probar desmayo (solo en Play)")]
    public void Trigger()
    {
        TriggerTo(_nextSceneName);
    }

    /// <summary>
    /// Igual que Trigger() pero hacia otra escena (UnityEvent con parametro
    /// string). Vacio = Next Scene Name.
    /// </summary>
    public void TriggerToScene(string sceneName)
    {
        TriggerTo(string.IsNullOrEmpty(sceneName) ? _nextSceneName : sceneName);
    }

    private void TriggerTo(string sceneName)
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[FaintTransition] El desmayo solo se puede probar en Play.");
            return;
        }
        if (_triggered || FaintOverlay.IsRunning)
        {
            Debug.Log("[FaintTransition] " + name + ": ya hay un desmayo en curso, se ignora.");
            return;
        }
        if (!CanLoad(sceneName))
        {
            Debug.LogError("[FaintTransition] " + name + ": no se dispara el desmayo, la escena '" + sceneName + "' no esta agregada/tildada en File > Build Settings (o el nombre no coincide exacto).");
            return;
        }
        _triggered = true;
        Debug.Log("[FaintTransition] " + name + ": desmayo -> '" + sceneName + "'.");

        // Nada mas se puede mirar/seleccionar durante el colapso.
        foreach (GazeController gaze in FindObjectsByType<GazeController>(FindObjectsSortMode.None))
        {
            gaze.enabled = false;
        }

        // Un listener que tire una excepcion no puede cortar el desmayo.
        try
        {
            _onCollapseStart?.Invoke();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }

        if (_flickerLights != null)
        {
            foreach (FlickeringLight light in _flickerLights)
            {
                if (light != null)
                {
                    light.Burst(_preDelay + 1f);
                }
            }
        }

        PlayWhisper();

        FaintOverlay.Play(new FaintOverlay.Settings
        {
            SceneName = sceneName,
            Color = _color,
            FadeOutDuration = _fadeOutDuration,
            HoldSeconds = _holdSeconds,
            WakeDuration = _wakeDuration,
            BlinkOnWake = _blinkOnWake,
            ImpactClip = _impactClip,
            WakeClip = _wakeClip,
            Material = _overlayMaterial,
            PreDelay = Mathf.Max(0f, _preDelay),
            ShowText = _showLevelText,
            Title = _levelTitle,
            Subtitle = _levelSubtitle,
        });
    }

    private void PlayWhisper()
    {
        if (_whisperClip == null)
        {
            return;
        }
        Camera cam = Camera.main;
        if (cam == null)
        {
            return;
        }
        // Hijo de la camara: el susurro queda pegado al oido aunque se gire la cabeza.
        // PlayDelayed en vez de una corrutina: no depende de que este objeto siga activo.
        GameObject ear = new GameObject("Susurro");
        ear.transform.SetParent(cam.transform, false);
        ear.transform.localPosition = new Vector3(Mathf.Sign(_whisperSide) * 0.25f, 0f, 0f);
        AudioSource src = ear.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 1f;
        src.minDistance = 0.1f;
        src.maxDistance = 3f;
        src.rolloffMode = AudioRolloffMode.Linear;
        src.clip = _whisperClip;
        float delay = Mathf.Max(0f, _whisperDelay);
        src.PlayDelayed(delay);
        Destroy(ear, delay + _whisperClip.length + 0.1f);
    }

    private static bool CanLoad(string sceneName)
    {
        return !string.IsNullOrEmpty(sceneName) && Application.CanStreamedLevelBeLoaded(sceneName);
    }

#if UNITY_EDITOR
    // Al agregar el componente en el Editor, ya viene con el material correcto.
    private void Reset()
    {
        _overlayMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_ReticleAlwaysOnTop.mat");
    }
#endif
}
