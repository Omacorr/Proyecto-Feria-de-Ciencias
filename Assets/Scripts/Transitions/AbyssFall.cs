using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Final de la etapa 3 (Pasillo de hotel): la puerta del fondo no da a una
/// salida sino a un abismo negro; al dar un paso adentro, el jugador cae y se
/// desmaya.
///
/// Arma solo, al arrancar la escena, un POZO de paredes negras (shader
/// Custom/AbyssVoid: sin luz y sin niebla) del otro lado de la puerta. Mirando
/// desde el pasillo, el vano de la puerta se ve como negro infinito; al caer,
/// lo unico que se ve es el marco iluminado de la puerta alejandose hacia
/// arriba - eso es lo que hace que la caida se SIENTA en VR (en negro total
/// no se percibe ningun movimiento).
///
/// Como posicionarlo: poner este objeto en el UMBRAL de la puerta, a la
/// altura del PISO, centrado en el ancho del vano, con la flecha azul (Z)
/// apuntando hacia adentro del abismo. Seleccionandolo se ve el pozo como
/// caja de lineas en la Scene View.
/// Cablear el TeleportPoint que queda adentro del pozo:
/// On Player Arrived -> AbyssFall.StartFall().
///
/// Robustez (Main Camera separada de Player): la caida mueve solo la
/// posicion de Main Camera; PlayerFollowsCamera (si esta) copia solo X/Z, asi
/// que no pelea con la caida. Al empezar a caer se apaga la mirada y se
/// bloquea el TeleportManager (LockMovement) por su cuenta, aunque falte el
/// FaintTransition, para que ninguna caminata le pise la posicion a la camara.
/// </summary>
public class AbyssFall : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("El FaintTransition de esta escena (Next Scene Name = Parte 4 - Pasillo de madera). Su Pre Delay es cuanto dura la caida visible antes del negro: ~1.5 queda bien.")]
    [SerializeField] private FaintTransition _faint;
    [Tooltip("Main Camera. Si se deja vacio, usa Camera.main.")]
    [SerializeField] private Transform _cameraTransform;
    [Tooltip("Arrastrar Assets/Shaders/Abyss_Void.shader. Sin esto no se arma el pozo.")]
    [SerializeField] private Shader _voidShader;
    [Tooltip("TeleportManager de esta escena (vive bajo Player). Opcional: si se deja vacio se busca solo. Se bloquea al caer.")]
    [SerializeField] private TeleportManager _teleportManager;

    [Header("Pozo (medido desde el umbral, en metros)")]
    [SerializeField] private float _width = 3f;
    [SerializeField] private float _length = 3f;
    [Tooltip("Altura del pozo sobre el nivel del piso (hasta el techo).")]
    [SerializeField] private float _heightAbove = 3.5f;
    [Tooltip("Profundidad del pozo bajo el piso. Tiene que alcanzar para toda la caida.")]
    [SerializeField] private float _depth = 80f;
    [Tooltip("Objetos que se apagan al arrancar porque quedarian ADENTRO del pozo (por ejemplo, piso o muebles del cuarto detras de la puerta). Opcional.")]
    [SerializeField] private GameObject[] _hideInside;

    [Header("Vano de la puerta (visto desde ADENTRO del pozo)")]
    [Tooltip("Ancho del hueco de la puerta. Con ancho y alto > 0 se arma, del lado de la puerta y por ENCIMA del piso, una pared negra con un hueco de este tamaño. Hace falta porque las paredes de los modelos importados son de una sola cara: vistas desde atras (adentro del pozo, al caer) son invisibles y dejarian ver todo el pasillo en vez de solo el vano. Desde el pasillo no se ve (queda detras de la pared del hotel). 0 = no se arma (comportamiento viejo).")]
    [SerializeField] private float _doorwayWidth = 1.1f;
    [Tooltip("Alto del hueco de la puerta, medido desde el piso. 0 = no se arma la pared del vano.")]
    [SerializeField] private float _doorwayHeight = 2.2f;
    [Tooltip("Tildar SOLO si detras de la puerta el modelo tiene pared solida (sin hueco): arma un panel negro plano en el vano, un poco hacia el pasillo (Plug Offset), que queda tapado por la puerta cerrada y al abrirse se ve como negro infinito. Se apaga solo al empezar la caida, para que desde adentro del pozo se vea el vano iluminado alejandose.")]
    [SerializeField] private bool _plugDoorway;
    [Tooltip("Metros desde el umbral hacia el PASILLO donde va el panel negro de Plug Doorway. Tiene que quedar dentro del grosor de la puerta cerrada (para que no se vea hasta que se abra) y delante de la pared del modelo.")]
    [SerializeField] private float _plugOffset = 0.14f;

    [Header("Entrada al pozo (opcional, recomendado)")]
    [Tooltip("El GameObject del TeleportPoint que esta ADENTRO del pozo. Se apaga al arrancar y se prende recien cuando se abre la puerta (o cuando algo llama Arm()). Hace falta porque el rayo de la mirada ATRAVIESA las paredes del modelo (no tienen collider): sin esto, mirando la pared del pasillo en el angulo justo se podia elegir el punto del pozo y saltearse la llave.")]
    [SerializeField] private GameObject _entryPoint;
    [Tooltip("La puerta del fondo (el Transform que tiene el componente Door). Cuando gira mas de 'Door Open Angle' grados respecto de como estaba al arrancar, se habilita Entry Point. Si se deja vacio, Entry Point queda apagado hasta que algo llame Arm() desde un UnityEvent.")]
    [SerializeField] private Transform _doorToWatch;
    [Tooltip("Grados que tiene que girar la puerta para considerarla abierta.")]
    [SerializeField] private float _doorOpenAngle = 15f;

    [Header("Caida")]
    [Tooltip("Segundos parado 'en el aire' antes de empezar a caer (el paso).")]
    [SerializeField] private float _hangTime = 0.35f;
    [SerializeField] private float _gravity = 9.8f;
    [Tooltip("Sonido de la caida (viento, grito ahogado). 2D, opcional.")]
    [SerializeField] private AudioClip _fallClip;
    [Tooltip("Se dispara apenas empieza la caida (antes del Hang Time). Opcional: un grito del monstruo, apagar luces, etc.")]
    [SerializeField] private UnityEvent _onFallStart;

    private bool _falling;
    private bool _armed;
    private Quaternion _doorClosedRotation;
    private GameObject _plug;

    private void Start()
    {
        if (_hideInside != null)
        {
            foreach (GameObject go in _hideInside)
            {
                if (go != null)
                {
                    go.SetActive(false);
                }
            }
        }
        BuildShaft();

        if (_entryPoint != null)
        {
            _entryPoint.SetActive(false);
            if (_doorToWatch != null)
            {
                _doorClosedRotation = _doorToWatch.rotation;
            }
            else
            {
                Debug.Log("[AbyssFall] Entry Point queda apagado hasta que algo llame Arm() (no hay Door To Watch asignado).");
            }
        }
    }

    private void Update()
    {
        if (_armed || _entryPoint == null || _doorToWatch == null)
        {
            return;
        }
        if (Quaternion.Angle(_doorClosedRotation, _doorToWatch.rotation) > _doorOpenAngle)
        {
            Arm();
        }
    }

    /// <summary>
    /// Habilita el TeleportPoint de adentro del pozo (Entry Point). Se llama
    /// sola cuando se abre Door To Watch; tambien se puede llamar desde un
    /// UnityEvent.
    /// </summary>
    public void Arm()
    {
        if (_armed)
        {
            return;
        }
        _armed = true;
        if (_entryPoint != null)
        {
            _entryPoint.SetActive(true);
            Debug.Log("[AbyssFall] Puerta abierta: se habilita el punto del pozo.");
        }
    }

    public void StartFall()
    {
        if (_falling)
        {
            return;
        }
        _falling = true;
        Debug.Log("[AbyssFall] Cayendo al abismo.");

        // Nada de mirar/teletransportarse durante la caida, aunque falte el
        // FaintTransition (que tambien apaga la mirada).
        foreach (GazeController gaze in FindObjectsByType<GazeController>(FindObjectsSortMode.None))
        {
            gaze.enabled = false;
        }
        TeleportManager manager = _teleportManager != null ? _teleportManager : FindFirstObjectByType<TeleportManager>();
        if (manager != null)
        {
            manager.LockMovement();
        }

        // El jugador ya esta adentro del pozo: sin el tapon, desde aca se ve el
        // vano iluminado alejandose hacia arriba mientras cae.
        if (_plug != null)
        {
            _plug.SetActive(false);
        }

        _onFallStart?.Invoke();

        Transform cam = _cameraTransform != null ? _cameraTransform : (Camera.main != null ? Camera.main.transform : null);
        if (cam == null)
        {
            Debug.LogWarning("[AbyssFall] No hay camara para hacer caer.");
        }
        else
        {
            // Mirando hacia abajo al caer tiene que verse negro, no el skybox.
            Camera c = cam.GetComponent<Camera>();
            if (c != null)
            {
                c.clearFlags = CameraClearFlags.SolidColor;
                c.backgroundColor = Color.black;
            }
            StartCoroutine(Fall(cam));
        }

        if (_fallClip != null)
        {
            AudioSource src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            src.PlayOneShot(_fallClip);
        }

        if (_faint != null)
        {
            _faint.Trigger();
        }
        else
        {
            Debug.LogWarning("[AbyssFall] Falta asignar Faint: el jugador cae pero no pasa de nivel.");
        }
    }

    private IEnumerator Fall(Transform cam)
    {
        if (_hangTime > 0f)
        {
            yield return new WaitForSeconds(_hangTime);
        }
        float velocity = 0f;
        float floorY = transform.position.y;
        float bottom = floorY - _depth + 1f;
        while (cam != null && cam.position.y > bottom)
        {
            velocity += _gravity * Time.deltaTime;
            cam.position += Vector3.down * velocity * Time.deltaTime;
            yield return null;
        }
    }

    // ---------------- Pozo ----------------

    private void BuildShaft()
    {
        if (_voidShader == null)
        {
            Debug.LogWarning("[AbyssFall] Falta asignar Void Shader (Assets/Shaders/Abyss_Void.shader): no se arma el pozo.");
            return;
        }
        Material mat = new Material(_voidShader);

        GameObject root = new GameObject("Pozo (generado)");
        root.transform.SetParent(transform, false);

        foreach (Bounds b in ShaftPieces())
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Pared";
            Destroy(wall.GetComponent<Collider>()); // que no frene la mirada
            wall.transform.SetParent(root.transform, false);
            wall.transform.localPosition = b.center;
            wall.transform.localScale = b.size;
            MeshRenderer mr = wall.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        if (_plugDoorway && _doorwayWidth > 0f && _doorwayHeight > 0f)
        {
            float dw = Mathf.Min(_doorwayWidth, _width);
            float dh = Mathf.Min(_doorwayHeight, _heightAbove);
            _plug = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _plug.name = "Tapon del vano";
            Destroy(_plug.GetComponent<Collider>()); // la mirada lo atraviesa hasta el punto del pozo
            _plug.transform.SetParent(root.transform, false);
            _plug.transform.localPosition = new Vector3(0f, dh / 2f, -_plugOffset);
            _plug.transform.localScale = new Vector3(dw, dh, 1f);
            MeshRenderer pr = _plug.GetComponent<MeshRenderer>();
            pr.sharedMaterial = mat;
            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pr.receiveShadows = false;
        }
    }

    /// <summary>Paredes del pozo en espacio LOCAL (Z = hacia adentro del abismo).</summary>
    private List<Bounds> ShaftPieces()
    {
        const float t = 0.1f;
        float w = _width, l = _length, h = _heightAbove, d = _depth;
        float midY = (h - d) / 2f;
        var pieces = new List<Bounds>
        {
            new Bounds(new Vector3(0f, midY, l), new Vector3(w, h + d, t)),            // fondo
            new Bounds(new Vector3(-w / 2f, midY, l / 2f), new Vector3(t, h + d, l)),   // izquierda
            new Bounds(new Vector3(w / 2f, midY, l / 2f), new Vector3(t, h + d, l)),    // derecha
            new Bounds(new Vector3(0f, h, l / 2f), new Vector3(w, t, l)),               // techo
            new Bounds(new Vector3(0f, -d, l / 2f), new Vector3(w, t, l)),              // fondo del pozo
            // Lado de la puerta, por debajo del piso.
            new Bounds(new Vector3(0f, -d / 2f, 0.05f), new Vector3(w, d, t)),
        };

        // Lado de la puerta, por ENCIMA del piso: pared negra con el hueco del
        // vano (ver tooltip de Doorway Width).
        if (_doorwayWidth > 0f && _doorwayHeight > 0f)
        {
            float dw = Mathf.Min(_doorwayWidth, w);
            float dh = Mathf.Min(_doorwayHeight, h);
            float side = (w - dw) / 2f;
            if (side > 0.001f)
            {
                pieces.Add(new Bounds(new Vector3(-(dw + side) / 2f, h / 2f, 0.05f), new Vector3(side, h, t)));
                pieces.Add(new Bounds(new Vector3((dw + side) / 2f, h / 2f, 0.05f), new Vector3(side, h, t)));
            }
            if (h - dh > 0.001f)
            {
                pieces.Add(new Bounds(new Vector3(0f, (dh + h) / 2f, 0.05f), new Vector3(dw, h - dh, t)));
            }
        }
        return pieces;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.8f);
        foreach (Bounds b in ShaftPieces())
        {
            Gizmos.DrawWireCube(b.center, b.size);
        }
        // Vano de la puerta (verde) y direccion hacia adentro del abismo.
        if (_doorwayWidth > 0f && _doorwayHeight > 0f)
        {
            Gizmos.color = new Color(0.2f, 1f, 0.3f, 0.9f);
            Gizmos.DrawWireCube(new Vector3(0f, _doorwayHeight / 2f, 0f), new Vector3(_doorwayWidth, _doorwayHeight, 0.02f));
        }
        Gizmos.DrawLine(Vector3.zero, Vector3.forward * 1f);
    }
}
