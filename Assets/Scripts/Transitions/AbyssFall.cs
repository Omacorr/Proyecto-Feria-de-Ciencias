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

    [Header("Vista de la caida: referencias en las paredes")]
    [Tooltip("Arma tiras (vigas/grietas) en las paredes del pozo, BAJO el nivel del piso, para que se perciba el movimiento al mirar hacia adelante. Es UNA sola malla con un submesh por tramo de profundidad (pocos draw calls). Usa Void Shader: sin luz y sin niebla.")]
    [SerializeField] private bool _wallBands = true;
    [Tooltip("Metros entre vigas horizontales (con algo de desorden).")]
    [SerializeField, Min(0.5f)] private float _bandSpacing = 2f;
    [Tooltip("Alto de cada viga (metros).")]
    [SerializeField, Min(0.02f)] private float _bandHeight = 0.22f;
    [Tooltip("Cuanto se separan de la pared hacia adentro del pozo (evita el z-fighting con la pared negra).")]
    [SerializeField, Min(0.02f)] private float _bandInset = 0.07f;
    [Tooltip("Metros bajo el piso donde empieza la primera viga (para no cambiar lo que se ve desde el pasillo).")]
    [SerializeField, Min(0f)] private float _bandStartBelowFloor = 0.8f;
    [Tooltip("Color de las vigas arriba del pozo (HDR; gris pardo oscuro). Se oscurece con la profundidad hasta casi negro.")]
    [ColorUsage(false, true)]
    [SerializeField] private Color _bandColor = new Color(0.16f, 0.12f, 0.09f, 1f);
    [Tooltip("Cantidad de tramos de profundidad (cada uno es un submesh/material con un color mas oscuro).")]
    [SerializeField, Range(1, 8)] private int _depthTiers = 4;
    [Tooltip("Grietas verticales al azar repartidas por las paredes (0 = ninguna).")]
    [SerializeField, Min(0)] private int _crackCount = 24;
    [Tooltip("Semilla del desorden: el mismo valor da siempre el mismo pozo.")]
    [SerializeField] private int _randomSeed = 7;

    [Header("Vista de la caida: polvo y escombros")]
    [Tooltip("Particulas de polvo que acompanan a la camara durante la caida (se ven subir). Se crean por codigo.")]
    [SerializeField] private bool _dust = true;
    [Tooltip("Material de las particulas (Particles/Unlit de URP, de preferencia). Asignarlo: sin esto se usa Shader.Find (gotcha #25) y, si falla, un cuadradito de Void Shader.")]
    [SerializeField] private Material _dustMaterial;
    [Tooltip("Cantidad maxima de particulas vivas.")]
    [SerializeField, Range(8, 120)] private int _dustMaxParticles = 48;
    [Tooltip("Tamano de cada particula (metros).")]
    [SerializeField, Min(0.005f)] private float _dustSize = 0.05f;
    [ColorUsage(false, true)]
    [SerializeField] private Color _dustColor = new Color(0.45f, 0.38f, 0.30f, 1f);

    [Header("Vista de la caida: camara")]
    [Tooltip("Grados que se inclina la vista hacia ARRIBA al empezar a caer, para que se vea el vano alejandose. 0 = no inclina.")]
    [SerializeField, Range(0f, 60f)] private float _lookUpPitch = 25f;
    [Tooltip("Segundos que tarda en inclinarse.")]
    [SerializeField, Min(0.01f)] private float _pitchSeconds = 0.4f;
    [Tooltip("Sacudida de camara al empezar (metros de desplazamiento maximo). 0 = ninguna.")]
    [SerializeField, Min(0f)] private float _shakeAmount = 0.06f;
    [Tooltip("Segundos que dura la sacudida (decae sola).")]
    [SerializeField, Min(0.01f)] private float _shakeSeconds = 0.5f;

    private bool _falling;
    private bool _armed;
    private Quaternion _doorClosedRotation;
    private GameObject _plug;
    private Transform _cam;
    private Transform _camOriginalParent;
    private Transform _pivot;
    private GameObject _dustObject;
    private Material _dustFallbackMat;
    private Material[] _bandMaterials;
    private Material _voidMaterial;

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

    private void LateUpdate()
    {
        if (_dustObject != null && _cam != null)
        {
            _dustObject.transform.position = _cam.position;
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
            StartDust();
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
        // Pivote temporal: el TrackedPoseDriver pisa la rotacion LOCAL de la
        // camara en cada frame, asi que la inclinacion y la sacudida se aplican
        // a un padre vacio (la camara conserva su pose mundial al reparentarse).
        _cam = cam;
        _camOriginalParent = cam.parent;
        GameObject pivotGo = new GameObject("Pivote de caida (generado)");
        _pivot = pivotGo.transform;
        _pivot.SetParent(_camOriginalParent, true);
        _pivot.position = cam.position;
        _pivot.localRotation = Quaternion.identity; // igual que el padre original: la rotacion local de la camara sigue valiendo
        cam.SetParent(_pivot, true);

        Vector3 basePos = _pivot.position;
        float velocity = 0f;
        float floorY = transform.position.y;
        float bottom = floorY - _depth + 1f;
        float t = 0f;
        float hangLeft = _hangTime;

        while (_pivot != null && basePos.y > bottom)
        {
            float dt = Time.deltaTime;
            t += dt;

            // Inclinacion hacia arriba, sobre el eje "derecha" horizontal segun
            // hacia donde mira la cabeza (si no, mirando de costado seria un roll).
            if (_lookUpPitch > 0f)
            {
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / _pitchSeconds));
                Quaternion yaw = Quaternion.Euler(0f, cam.localEulerAngles.y, 0f);
                _pivot.localRotation = Quaternion.AngleAxis(-_lookUpPitch * k, yaw * Vector3.right);
            }

            if (hangLeft > 0f)
            {
                hangLeft -= dt;
            }
            else
            {
                velocity += _gravity * dt;
                basePos += Vector3.down * velocity * dt;
            }

            Vector3 shake = Vector3.zero;
            if (_shakeAmount > 0f && t < _shakeSeconds)
            {
                float decay = 1f - t / _shakeSeconds;
                shake = Random.insideUnitSphere * (_shakeAmount * decay);
            }
            _pivot.position = basePos + shake;
            yield return null;
        }
    }

    private void OnDestroy()
    {
        // Entregar la camara a su padre original (si sigue viva) antes de que
        // se destruya el pivote; seguro si la escena se esta descargando.
        if (_cam != null && _pivot != null && _cam.parent == _pivot)
        {
            _cam.SetParent(_camOriginalParent, true);
        }
        if (_pivot != null)
        {
            Destroy(_pivot.gameObject);
        }
        if (_dustObject != null)
        {
            Destroy(_dustObject);
        }
        if (_dustFallbackMat != null)
        {
            Destroy(_dustFallbackMat);
        }
        if (_voidMaterial != null)
        {
            Destroy(_voidMaterial);
        }
        if (_bandMaterials != null)
        {
            foreach (Material m in _bandMaterials)
            {
                if (m != null)
                {
                    Destroy(m);
                }
            }
        }
    }

    // ---------------- Polvo ----------------

    private void StartDust()
    {
        if (!_dust)
        {
            return;
        }
        Material mat = ResolveDustMaterial();
        if (mat == null)
        {
            return;
        }

        _dustObject = new GameObject("Polvo de caida (generado)");
        // El emisor sigue la POSICION de la camara (ver LateUpdate) pero no su
        // rotacion, para que la caja de emision quede siempre "hacia abajo".
        // Las particulas se simulan en espacio MUNDO: quedan quietas y la
        // camara las pasa, asi que se ven subir.
        if (_cam != null)
        {
            _dustObject.transform.position = _cam.position;
        }
        _dustObject.transform.rotation = Quaternion.identity;

        ParticleSystem ps = _dustObject.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.5f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(_dustSize * 0.5f, _dustSize);
        main.startColor = _dustColor;
        main.maxParticles = _dustMaxParticles;
        main.gravityModifier = 0f;

        ParticleSystem.EmissionModule em = ps.emission;
        em.rateOverTime = _dustMaxParticles * 0.8f;

        // Caja delante de la caida (hacia abajo) y dentro del ancho del pozo.
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        float side = Mathf.Max(0.5f, Mathf.Min(_width, _length) - 0.4f);
        shape.scale = new Vector3(side, 10f, side);
        shape.position = new Vector3(0f, -7f, 0f);
        shape.rotation = Vector3.zero;

        ParticleSystemRenderer pr = _dustObject.GetComponent<ParticleSystemRenderer>();
        pr.sharedMaterial = mat;
        pr.renderMode = ParticleSystemRenderMode.Billboard;
        pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        pr.receiveShadows = false;

        ps.Play();
    }

    private Material ResolveDustMaterial()
    {
        if (_dustMaterial != null)
        {
            return _dustMaterial;
        }
        Debug.LogWarning("[AbyssFall] Falta asignar Dust Material: uso Shader.Find('Universal Render Pipeline/Particles/Unlit'), que puede devolver null en el APK (gotcha #25).");
        Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (sh != null)
        {
            _dustFallbackMat = new Material(sh);
            _dustFallbackMat.SetColor("_BaseColor", Color.white);
            return _dustFallbackMat;
        }
        if (_voidShader != null)
        {
            // Ultimo recurso (el shader esta serializado, siempre esta en el build):
            // cuadraditos opacos de un solo color.
            _dustFallbackMat = new Material(_voidShader);
            _dustFallbackMat.SetColor("_Color", _dustColor);
            return _dustFallbackMat;
        }
        Debug.LogWarning("[AbyssFall] Sin shader para el polvo: no se crean particulas.");
        return null;
    }

    // ---------------- Vigas y grietas ----------------

    /// <summary>
    /// Una sola malla con quads (un submesh por tramo de profundidad) sobre las
    /// paredes del pozo. Local: Z hacia adentro, Y arriba, piso en Y = 0.
    /// </summary>
    private void BuildBands(Transform root)
    {
        int tiers = Mathf.Clamp(_depthTiers, 1, 8);
        var verts = new List<Vector3>();
        var tris = new List<int>[tiers];
        for (int i = 0; i < tiers; i++)
        {
            tris[i] = new List<int>();
        }

        float hw = _width / 2f - 0.05f - _bandInset; // cara interior de las paredes laterales, hacia adentro
        float zBack = _length - 0.05f - _bandInset;
        float zFront = 0.1f + _bandInset;            // cara interior del lado de la puerta
        float yTop = -_bandStartBelowFloor;
        float yBottom = -_depth + 1f;
        float span = Mathf.Max(0.01f, yTop - yBottom);
        var rng = new System.Random(_randomSeed);

        System.Func<double> rnd = () => rng.NextDouble();

        // Vigas horizontales: cada _bandSpacing, con desorden; cada pared tiene
        // las suyas, a veces cortadas (no cubren todo el ancho).
        for (float y = yTop; y > yBottom; y -= _bandSpacing * (0.6f + (float)rnd() * 0.8f))
        {
            int tier = Mathf.Min(tiers - 1, (int)((yTop - y) / span * tiers));
            float h = _bandHeight * (0.6f + (float)rnd() * 0.9f);
            for (int wall = 0; wall < 4; wall++)
            {
                if (rnd() < 0.15)
                {
                    continue; // hueco: no todas las paredes tienen viga en cada nivel
                }
                float len = (wall < 2 ? _length : _width) - 0.4f;
                float a = (float)rnd() * 0.35f;
                float b = 1f - (float)rnd() * 0.35f;
                float u0 = -len / 2f + len * a;
                float u1 = -len / 2f + len * b;
                AddWallQuad(verts, tris[tier], wall, hw, zBack, zFront, u0, u1, y - h, y);
            }
        }

        // Grietas verticales.
        for (int i = 0; i < _crackCount; i++)
        {
            int wall = rng.Next(0, 4);
            float len = (wall < 2 ? _length : _width) - 0.4f;
            float u = -len / 2f + (float)rnd() * len;
            float cl = 1f + (float)rnd() * 3f;
            float yTopC = yTop - (float)rnd() * (span - cl);
            int tier = Mathf.Min(tiers - 1, (int)((yTop - yTopC) / span * tiers));
            float wdt = 0.03f + (float)rnd() * 0.04f;
            AddWallQuad(verts, tris[tier], wall, hw, zBack, zFront, u - wdt, u + wdt, yTopC - cl, yTopC);
        }

        var mesh = new Mesh { name = "Vigas del pozo (generado)" };
        mesh.SetVertices(verts);
        mesh.subMeshCount = tiers;
        for (int i = 0; i < tiers; i++)
        {
            mesh.SetTriangles(tris[i], i);
        }
        mesh.RecalculateBounds();

        _bandMaterials = new Material[tiers];
        for (int i = 0; i < tiers; i++)
        {
            // Cuanto mas hondo, mas oscuro (hasta casi negro): el fondo se pierde.
            float k = 1f - (i / (float)tiers);
            _bandMaterials[i] = new Material(_voidShader);
            _bandMaterials[i].SetColor("_Color", _bandColor * (k * k));
        }

        GameObject go = new GameObject("Vigas del pozo");
        go.transform.SetParent(root, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterials = _bandMaterials;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }

    /// <summary>
    /// Pared 0 = izquierda, 1 = derecha, 2 = fondo, 3 = lado de la puerta.
    /// u recorre la pared (Z en las laterales, X en fondo/puerta).
    /// </summary>
    private static void AddWallQuad(List<Vector3> v, List<int> t, int wall, float hw, float zBack, float zFront, float u0, float u1, float y0, float y1)
    {
        Vector3 a, b, c, d;
        switch (wall)
        {
            case 0:
                a = new Vector3(-hw, y0, ZAlong(u0, zBack, zFront)); b = new Vector3(-hw, y0, ZAlong(u1, zBack, zFront));
                c = new Vector3(-hw, y1, ZAlong(u1, zBack, zFront)); d = new Vector3(-hw, y1, ZAlong(u0, zBack, zFront));
                break;
            case 1:
                a = new Vector3(hw, y0, ZAlong(u0, zBack, zFront)); b = new Vector3(hw, y0, ZAlong(u1, zBack, zFront));
                c = new Vector3(hw, y1, ZAlong(u1, zBack, zFront)); d = new Vector3(hw, y1, ZAlong(u0, zBack, zFront));
                break;
            case 2:
                a = new Vector3(u0, y0, zBack); b = new Vector3(u1, y0, zBack);
                c = new Vector3(u1, y1, zBack); d = new Vector3(u0, y1, zBack);
                break;
            default:
                a = new Vector3(u0, y0, zFront); b = new Vector3(u1, y0, zFront);
                c = new Vector3(u1, y1, zFront); d = new Vector3(u0, y1, zFront);
                break;
        }
        int i = v.Count;
        v.Add(a); v.Add(b); v.Add(c); v.Add(d);
        t.Add(i); t.Add(i + 1); t.Add(i + 2);
        t.Add(i); t.Add(i + 2); t.Add(i + 3);
    }

    // u en [-len/2, len/2] -> Z entre zFront y zBack.
    private static float ZAlong(float u, float zBack, float zFront)
    {
        float len = zBack - zFront;
        return zFront + len / 2f + u;
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
        _voidMaterial = mat;

        GameObject root = new GameObject("Pozo (generado)");
        root.transform.SetParent(transform, false);

        if (_wallBands)
        {
            BuildBands(root.transform);
        }

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
