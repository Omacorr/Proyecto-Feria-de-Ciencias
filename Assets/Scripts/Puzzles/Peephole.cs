using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;

/// <summary>
/// Mirilla de una puerta (Etapa 3, "Pasillo de hotel"). Al mirarla y sostener
/// la mirada, el jugador "acerca el ojo": fundido a negro, la camara pasa a un
/// punto fijo DEL OTRO LADO de la puerta (View Anchor) y se ve el cuarto a
/// traves de un circulo negro (mascara de mirilla). Mismo criterio que
/// ExamineTrigger (gotcha #3 de CLAUDE.md): no se pega nada a la camara para
/// acercarlo, se mueve al JUGADOR, asi se puede seguir apuntando con la mirada
/// a cosas del cuarto (por ejemplo, la llave del piso, que es un Collectable
/// comun y se junta mirandola a traves de la mirilla).
///
/// Diferencias con ExamineTrigger, a proposito:
///  - No usa TeleportManager para moverse: la escena puede tener "Use Walk
///    Animation" tildado, y caminar desde el pasillo hasta un cuarto (o de
///    vuelta) atravesaria paredes. Aca siempre es fundido + salto, ida y vuelta,
///    y al volver el jugador queda EXACTAMENTE donde estaba (TeleportManager ni
///    se entera, su CurrentOccupiedPoint sigue siendo el correcto).
///  - Mientras se espia, apaga (y despues restaura) los Collider de TODOS los
///    TeleportPoint de la escena, para que no se pueda seleccionar un punto del
///    pasillo o del cuarto "a traves" de la mirilla y salir caminando por
///    dentro de las paredes.
///  - Para salir no hace falta ningun boton: alcanza con girar la cabeza lejos
///    de la mirilla (Look Away Angle), hay un tiempo maximo de seguridad, y
///    ExitPeephole()/ExitAfterDelay() se pueden llamar desde un UnityEvent (por
///    ejemplo, Collectable.On Collected de la llave).
///
/// Mirilla "ciega" (sin View Anchor): no mueve al jugador, solo muestra la
/// mascara con el centro negro unos segundos (opcionalmente con un sonido del
/// otro lado de la puerta) y vuelve. Sirve para las puertas "falsas" del pasillo
/// sin tener que armar un cuarto detras de cada una.
///
/// Requisitos de escena: Collider + Layer Interactive (7) en este objeto (o en
/// un hijo), igual que cualquier IGazeInteractable. Evitar escala no uniforme
/// en este objeto (ver "Portales sin interaccion..." en CLAUDE.md).
/// </summary>
public class Peephole : MonoBehaviour, IGazeInteractable
{
    [Header("Referencias")]
    [Tooltip("Main Camera de la escena (la que mueve TeleportManager, NO Player). Si se deja vacio, usa Camera.main.")]
    [SerializeField] private Transform _cameraTransform;

    [Tooltip("Opcional pero recomendado: el TeleportManager de la escena. Si esta asignado, no deja espiar mientras el jugador esta caminando o teletransportandose.")]
    [SerializeField] private TeleportManager _teleportManager;

    [Tooltip("Opcional pero recomendado: el VRFadeController de la escena (hijo de Player). Si se deja vacio, el cambio de vista es instantaneo, sin fundido.")]
    [SerializeField] private VRFadeController _fadeController;

    [Header("Vista")]
    [Tooltip("Objeto VACIO (Create Empty) puesto del OTRO lado de la puerta, a la altura de los ojos, donde queda la camara mientras se espia. Solo importa su POSICION: la rotacion la maneja el casco, asi que el jugador ve el cuarto mirando en la misma direccion en que miraba la puerta. Dejalo vacio para una mirilla 'ciega' (solo negro).")]
    [SerializeField] private Transform _viewAnchor;

    [Tooltip("Solo mirilla ciega (sin View Anchor): segundos que se ve todo negro antes de volver solo.")]
    [SerializeField] private float _blindDuration = 2.5f;

    [Header("Salida")]
    [Tooltip("Si el jugador gira la cabeza mas de estos grados respecto de hacia donde miraba al entrar, 'aparta el ojo' y vuelve al pasillo. 0 = desactivado (solo sale por tiempo o por ExitPeephole).")]
    [SerializeField] private float _lookAwayAngle = 80f;

    [Tooltip("Cuantos segundos seguidos tiene que estar mirando lejos para salir (evita salidas por un movimiento brusco).")]
    [SerializeField] private float _lookAwayHoldSeconds = 0.5f;

    [Tooltip("Tiempo maximo espiando (seguridad, para que nadie quede trabado). 0 = sin limite.")]
    [SerializeField] private float _maxPeepSeconds = 20f;

    [Header("Uso")]
    [Tooltip("Distancia maxima (horizontal, en unidades de la escena) entre la camara y la mirilla para poder usarla. 0 = sin limite (alcanza con que el gaze llegue).")]
    [SerializeField] private float _maxUseDistance = 0f;

    [Tooltip("Tildado (recomendado): mientras se espia se apagan los Collider de todos los TeleportPoint de la escena, y se restauran al volver.")]
    [SerializeField] private bool _blockTeleportPointsWhilePeeping = true;

    [Header("Mascara de mirilla")]
    [Tooltip("Muestra el circulo negro de mirilla delante de la camara mientras se espia.")]
    [SerializeField] private bool _showMask = true;

    [Tooltip("Arrastrar Assets/Materials/Mat_ReticleAlwaysOnTop (shader UI/AlwaysOnTop): asi la mascara no queda tapada por geometria cercana. Si se deja vacio, se busca el shader por nombre.")]
    [SerializeField] private Material _maskMaterial;

    [Tooltip("Radio del agujero de la mirilla, en grados desde el centro de la vista. 20-25 se siente como una mirilla real.")]
    [Range(5f, 45f)]
    [SerializeField] private float _holeAngle = 22f;

    [Tooltip("Borde difuso del agujero, como fraccion del radio (0.15 = 15%).")]
    [Range(0f, 1f)]
    [SerializeField] private float _holeFeather = 0.2f;

    [Tooltip("Distancia de la mascara a la camara. Tiene que quedar DELANTE del ReticleCanvas (1.5) para que el punto de mira se vea dentro del agujero.")]
    [SerializeField] private float _maskDistance = 1.2f;

    [Header("Audio (opcional)")]
    [Tooltip("AudioSource desde donde suenan los clips (conviene uno 2D o pegado a la puerta). Si falta, no suena nada.")]
    [SerializeField] private AudioSource _audioSource;

    [Tooltip("Sonido al acercar el ojo (roce, respiracion).")]
    [SerializeField] private AudioClip _peepSound;

    [Tooltip("Solo mirilla ciega: sonido del otro lado de la puerta (golpe, respiracion, pasos).")]
    [SerializeField] private AudioClip _blindSound;

    [Header("Feedback de mirada (opcional)")]
    [SerializeField] private Material _inactiveMaterial;
    [SerializeField] private Material _gazedAtMaterial;

    [Header("Eventos")]
    [Tooltip("Se dispara con la pantalla en negro, justo al 'acercar el ojo' (antes de que se vea el cuarto). Ideal para PRENDER la luz roja del cuarto (y apagarla en On Peep End: una Light sin sombras atraviesa paredes e iluminaria el pasillo).")]
    [SerializeField] private UnityEvent _onPeepStart;

    [Tooltip("Se dispara con la pantalla en negro, justo al volver al pasillo.")]
    [SerializeField] private UnityEvent _onPeepEnd;

    /// <summary>True mientras el jugador esta espiando por ESTA mirilla.</summary>
    public bool IsPeeping { get; private set; }

    // Una sola mirilla activa a la vez en toda la escena.
    private static Peephole s_active;

    private const int MaskSegments = 64;

    private Renderer _renderer;
    private bool _needsGazeExit;
    private float _returnedAt;
    private bool _exitRequested;
    private bool _cameraMoved;
    private Vector3 _returnPosition;
    private Vector3 _referenceForward;
    private Coroutine _peepRoutine;
    private Coroutine _delayedExit;

    private readonly List<Collider> _blockedColliders = new List<Collider>();
    private readonly List<GazeController> _disabledGaze = new List<GazeController>();

    private GameObject _maskObject;
    private Mesh _maskMesh;
    private Material _maskMaterialInstance;

    // Por si el Editor tiene desactivado el "Domain Reload" al entrar a Play:
    // que no quede una mirilla "activa" colgada de la sesion anterior.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        s_active = null;
    }

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        SetGazed(false);
    }

    private void OnDisable()
    {
        // Si este objeto se apaga (o se descarga la escena) en medio de una
        // espiada, la corrutina muere: devolvemos todo a su lugar sin fundido
        // para no dejar al jugador encerrado del otro lado de la puerta.
        if (IsPeeping)
        {
            RestoreAfterPeep();
            FinishPeep();
        }
    }

    private void OnDestroy()
    {
        if (_maskObject != null)
        {
            Destroy(_maskObject);
        }
        if (_maskMesh != null)
        {
            Destroy(_maskMesh);
        }
        if (_maskMaterialInstance != null)
        {
            Destroy(_maskMaterialInstance);
        }
    }

    public void OnGazeEnter()
    {
        // Si el jugador volvio mirando para otro lado (salio girando la
        // cabeza) y despues mira la mirilla a proposito, tiene que funcionar a
        // la primera. El caso a bloquear es solo volver YA mirandola: ahi este
        // OnGazeEnter llega durante el fundido de vuelta (IsPeeping todavia en
        // true) y el bloqueo se arma recien despues, en FinishPeep.
        if (_needsGazeExit && !IsPeeping && Time.time - _returnedAt > 0.5f)
        {
            _needsGazeExit = false;
        }
        SetGazed(true);
    }

    public void OnGazeStay(float progress)
    {
        // El feedback de progreso ya lo muestra GazeReticle.
    }

    public void OnGazeExit()
    {
        // Rearma la mirilla: despues de volver, hay que apartar la vista y
        // volver a mirarla para espiar de nuevo (si no, el jugador vuelve
        // mirando la mirilla y a los 2 segundos entraba otra vez solo).
        _needsGazeExit = false;
        SetGazed(false);
    }

    public void OnGazeSelect()
    {
        if (s_active != null || _needsGazeExit)
        {
            return;
        }

        if (_teleportManager != null && _teleportManager.IsTeleporting)
        {
            return;
        }

        if (_fadeController != null && _fadeController.IsFading)
        {
            return;
        }

        Transform cam = GetCameraTransform();
        if (cam == null)
        {
            Debug.LogWarning($"[Peephole] {gameObject.name}: no hay camara (asigna Camera Transform o pone el tag MainCamera).");
            return;
        }

        if (_maxUseDistance > 0f)
        {
            Vector3 offset = transform.position - cam.position;
            offset.y = 0f;
            if (offset.magnitude > _maxUseDistance)
            {
                Debug.Log($"[Peephole] {gameObject.name}: demasiado lejos para espiar ({offset.magnitude:0.0} > {_maxUseDistance}).");
                return;
            }
        }

        _peepRoutine = StartCoroutine(PeepRoutine(cam));
    }

    /// <summary>
    /// Vuelve al pasillo (con fundido). Llamable desde un UnityEvent, por
    /// ejemplo desde un boton "Volver" o desde el On Collected de la llave.
    /// </summary>
    public void ExitPeephole()
    {
        if (IsPeeping)
        {
            _exitRequested = true;
        }
    }

    /// <summary>
    /// Igual que ExitPeephole, pero espera unos segundos antes (para que se
    /// alcance a ver desaparecer la llave, por ejemplo). En el Inspector se
    /// elige con un valor float fijo.
    /// </summary>
    public void ExitAfterDelay(float seconds)
    {
        if (!IsPeeping)
        {
            return;
        }

        if (_delayedExit != null)
        {
            StopCoroutine(_delayedExit);
        }
        _delayedExit = StartCoroutine(DelayedExit(seconds));
    }

    private IEnumerator DelayedExit(float seconds)
    {
        if (seconds > 0f)
        {
            yield return new WaitForSeconds(seconds);
        }
        _delayedExit = null;
        ExitPeephole();
    }

    private IEnumerator PeepRoutine(Transform cam)
    {
        s_active = this;
        IsPeeping = true;
        _exitRequested = false;
        _cameraMoved = false;
        _returnPosition = cam.position;
        _referenceForward = cam.forward;
        bool blind = _viewAnchor == null;

        Play(_peepSound);
        Debug.Log($"[Peephole] {gameObject.name}: espiando ({(blind ? "mirilla ciega" : "vista -> " + _viewAnchor.name)}).");

        yield return Transition(() =>
        {
            // Si la mirilla se apago durante el fundido (OnDisable ya devolvio
            // todo a su lugar), no hay que volver a mover la camara.
            if (!IsPeeping)
            {
                return;
            }

            if (_blockTeleportPointsWhilePeeping)
            {
                SetTeleportPointsBlocked(true);
            }

            if (blind)
            {
                // Sin vista no hay nada que mirar: se bloquea la mirada para no
                // seleccionar algo del pasillo "a ciegas".
                SetGazeBlocked(true);
            }
            else
            {
                cam.position = _viewAnchor.position;
                _cameraMoved = true;
            }

            ShowMask(cam, blind);
            _onPeepStart?.Invoke();
        });

        if (blind)
        {
            Play(_blindSound);
        }

        float elapsed = 0f;
        float lookingAway = 0f;
        while (!_exitRequested)
        {
            elapsed += Time.deltaTime;

            if (blind)
            {
                if (elapsed >= _blindDuration)
                {
                    break;
                }
            }
            else
            {
                if (_maxPeepSeconds > 0f && elapsed >= _maxPeepSeconds)
                {
                    break;
                }

                if (_lookAwayAngle > 0f && cam != null)
                {
                    float angle = Vector3.Angle(_referenceForward, cam.forward);
                    lookingAway = angle > _lookAwayAngle ? lookingAway + Time.deltaTime : 0f;
                    if (lookingAway >= _lookAwayHoldSeconds)
                    {
                        break;
                    }
                }
            }

            yield return null;
        }

        yield return Transition(() =>
        {
            RestoreAfterPeep();
            _onPeepEnd?.Invoke();
        });

        FinishPeep();
    }

    /// <summary>
    /// Deshace todo lo que hizo la espiada: camara de vuelta, colliders,
    /// mirada y mascara. No dispara eventos.
    /// </summary>
    private void RestoreAfterPeep()
    {
        if (_cameraMoved)
        {
            Transform cam = GetCameraTransform();
            if (cam != null)
            {
                cam.position = _returnPosition;
            }
            _cameraMoved = false;
        }

        SetTeleportPointsBlocked(false);
        SetGazeBlocked(false);
        HideMask();
    }

    private void FinishPeep()
    {
        if (_delayedExit != null)
        {
            StopCoroutine(_delayedExit);
            _delayedExit = null;
        }

        IsPeeping = false;
        _exitRequested = false;
        _needsGazeExit = true;
        _returnedAt = Time.time;
        _peepRoutine = null;
        if (s_active == this)
        {
            s_active = null;
        }
        Debug.Log($"[Peephole] {gameObject.name}: de vuelta en el pasillo.");
    }

    private IEnumerator Transition(Action onBlackout)
    {
        if (_fadeController != null && _fadeController.isActiveAndEnabled && !_fadeController.IsFading)
        {
            // La corrutina del fundido corre en el propio VRFadeController: si
            // esta mirilla se apaga a mitad de camino, el fundido igual termina
            // y la pantalla no queda a medio negro.
            yield return _fadeController.StartCoroutine(_fadeController.FadeOutAndIn(onBlackout));
        }
        else
        {
            onBlackout?.Invoke();
        }
    }

    private Transform GetCameraTransform()
    {
        if (_cameraTransform != null)
        {
            return _cameraTransform;
        }
        Camera main = Camera.main;
        return main != null ? main.transform : null;
    }

    private void SetTeleportPointsBlocked(bool blocked)
    {
        if (blocked)
        {
            _blockedColliders.Clear();
            foreach (TeleportPoint point in FindObjectsByType<TeleportPoint>(FindObjectsSortMode.None))
            {
                foreach (Collider col in point.GetComponentsInChildren<Collider>())
                {
                    // Solo los que estan prendidos: el punto donde esta parado
                    // el jugador ya tiene el collider apagado por TeleportManager
                    // y tiene que seguir asi al volver.
                    if (col.enabled)
                    {
                        col.enabled = false;
                        _blockedColliders.Add(col);
                    }
                }
            }
            return;
        }

        foreach (Collider col in _blockedColliders)
        {
            if (col != null)
            {
                col.enabled = true;
            }
        }
        _blockedColliders.Clear();
    }

    private void SetGazeBlocked(bool blocked)
    {
        if (blocked)
        {
            _disabledGaze.Clear();
            foreach (GazeController gaze in FindObjectsByType<GazeController>(FindObjectsSortMode.None))
            {
                if (gaze.enabled)
                {
                    gaze.enabled = false;
                    _disabledGaze.Add(gaze);
                }
            }
            return;
        }

        foreach (GazeController gaze in _disabledGaze)
        {
            if (gaze != null)
            {
                gaze.enabled = true;
            }
        }
        _disabledGaze.Clear();
    }

    // ---------------------------------------------------------------------
    // Mascara: un disco negro con un agujero en el centro, hijo de la camara.
    // Es geometria de mundo (no un Canvas Screen Space, gotcha #7), sin
    // Collider y en la layer Ignore Raycast, asi que no molesta al gaze. Se
    // arma por codigo (no hace falta ninguna textura ni prefab).
    // ---------------------------------------------------------------------

    private void ShowMask(Transform cam, bool blind)
    {
        if (!_showMask || cam == null)
        {
            return;
        }

        if (_maskObject == null && !BuildMask())
        {
            return;
        }

        Camera camComponent = cam.GetComponent<Camera>();
        float distance = _maskDistance;
        if (camComponent != null)
        {
            distance = Mathf.Max(distance, camComponent.nearClipPlane * 2f);
        }

        // El mesh se arma para _maskDistance; si hubo que alejarlo por el near
        // clip, se escala parejo (escala uniforme) para mantener el angulo.
        float scale = distance / Mathf.Max(0.01f, _maskDistance);

        _maskObject.transform.SetParent(cam, false);
        _maskObject.transform.localPosition = new Vector3(0f, 0f, distance);
        _maskObject.transform.localRotation = Quaternion.identity;
        _maskObject.transform.localScale = new Vector3(scale, scale, scale);
        SetMaskHoleOpen(!blind);
        _maskObject.SetActive(true);
    }

    private void HideMask()
    {
        if (_maskObject != null)
        {
            _maskObject.SetActive(false);
        }
    }

    private bool BuildMask()
    {
        Material source = _maskMaterial;
        if (source == null)
        {
            Shader shader = Shader.Find("UI/AlwaysOnTop");
            if (shader == null)
            {
                shader = Shader.Find("UI/Default");
            }
            if (shader == null)
            {
                Debug.LogWarning("[Peephole] No encontre shader para la mascara: asigna Mat_ReticleAlwaysOnTop en Mask Material.");
                return false;
            }
            source = new Material(shader);
        }

        _maskMaterialInstance = new Material(source);
        _maskMaterialInstance.mainTexture = Texture2D.whiteTexture;
        _maskMaterialInstance.color = Color.black;
        // Despues de toda la escena, pero ANTES del reticle y del FadeImage
        // (queue Overlay = 4000): asi el punto de mira se sigue viendo en el
        // agujero y el fundido a negro tapa tambien a la mascara.
        _maskMaterialInstance.renderQueue = 3999;

        _maskObject = new GameObject("PeepholeMask");
        _maskObject.layer = 2; // Ignore Raycast
        _maskObject.SetActive(false);

        _maskMesh = BuildMaskMesh();
        _maskObject.AddComponent<MeshFilter>().sharedMesh = _maskMesh;

        MeshRenderer mr = _maskObject.AddComponent<MeshRenderer>();
        mr.sharedMaterial = _maskMaterialInstance;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;
        return true;
    }

    private Mesh BuildMaskMesh()
    {
        int n = MaskSegments;
        float rInner = Mathf.Tan(_holeAngle * Mathf.Deg2Rad) * _maskDistance;
        float rFeather = rInner * (1f + Mathf.Max(0.01f, _holeFeather));
        // Suficiente para tapar todo el campo visual del Cardboard (~80 grados
        // desde el centro a esta distancia).
        float rOuter = _maskDistance * 8f;

        // Vertice 0 = centro; despues tres anillos: borde del agujero, fin del
        // difuminado y borde exterior.
        var vertices = new Vector3[1 + 3 * n];
        vertices[0] = Vector3.zero;
        for (int i = 0; i < n; i++)
        {
            float a = (float)i / n * Mathf.PI * 2f;
            var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
            vertices[1 + i] = dir * rInner;
            vertices[1 + n + i] = dir * rFeather;
            vertices[1 + 2 * n + i] = dir * rOuter;
        }

        var triangles = new List<int>(n * 15);
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            // Centro (se ve solo en la mirilla ciega).
            triangles.Add(0);
            triangles.Add(1 + i);
            triangles.Add(1 + j);
            // Difuminado.
            AddQuad(triangles, 1 + i, 1 + j, 1 + n + j, 1 + n + i);
            // Negro solido hasta afuera.
            AddQuad(triangles, 1 + n + i, 1 + n + j, 1 + 2 * n + j, 1 + 2 * n + i);
        }

        var mesh = new Mesh { name = "PeepholeMaskMesh" };
        mesh.vertices = vertices;
        mesh.SetTriangles(triangles, 0);
        mesh.colors = new Color[vertices.Length];
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void AddQuad(List<int> triangles, int a, int b, int c, int d)
    {
        triangles.Add(a);
        triangles.Add(b);
        triangles.Add(c);
        triangles.Add(a);
        triangles.Add(c);
        triangles.Add(d);
    }

    /// <summary>
    /// El shader UI/AlwaysOnTop multiplica textura * color de vertice * _Color
    /// (negro). El alpha de cada vertice decide que partes tapan: agujero
    /// transparente (mirilla con vista) o todo negro (mirilla ciega).
    /// </summary>
    private void SetMaskHoleOpen(bool open)
    {
        if (_maskMesh == null)
        {
            return;
        }

        int n = MaskSegments;
        var colors = new Color[1 + 3 * n];
        var hole = new Color(1f, 1f, 1f, open ? 0f : 1f);
        var solid = new Color(1f, 1f, 1f, 1f);
        colors[0] = hole;
        for (int i = 0; i < n; i++)
        {
            colors[1 + i] = hole;
            colors[1 + n + i] = solid;
            colors[1 + 2 * n + i] = solid;
        }
        _maskMesh.colors = colors;
    }

    private void Play(AudioClip clip)
    {
        if (clip != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(clip);
        }
    }

    private void SetGazed(bool gazedAt)
    {
        if (_renderer != null && _inactiveMaterial != null && _gazedAtMaterial != null)
        {
            _renderer.material = gazedAt ? _gazedAtMaterial : _inactiveMaterial;
        }
    }
}
