using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Final de la etapa 4 (Pasillo de madera sin puertas): la pared ciega se
/// agrieta, se desmorona y detras aparece una luz amarilla cegadora que
/// "absorbe" al jugador (FaintTransition en blanco-amarillo -> Parte 5).
///
/// La pared real del modelo (house_corridor_interior) es UNA sola malla para
/// todo el edificio, asi que no se puede romper un pedazo. En cambio, este
/// script arma al arrancar una pared FALSA de bloques pegada adelante de la
/// pared ciega (con el material de la pared del pasillo), y entre esa pared
/// falsa y la real un panel que brilla (color HDR + Bloom). Al derrumbarse
/// los bloques, queda a la vista el panel brillante.
///
/// Como posicionarlo: poner este objeto sobre la cara VISIBLE de la pared
/// ciega (la que da al pasillo), a la altura del PISO y centrado en el ancho,
/// con la flecha azul (Z) apuntando hacia el pasillo (hacia el jugador).
/// Seleccionandolo se ve el area en la Scene View.
///
/// OJO con las paredes gruesas: en Parte 4 la pared ciega mide 0.6 m de
/// espesor (cara visible en x=-136.31, cara exterior en x=-136.91). Un
/// Physics.Raycast normal puede saltearse la cara visible (el padre de la
/// malla tiene escala negativa -espejada- y el material es de doble cara:
/// la fisica puede tratarla como cara trasera) y devolver la exterior. Si el pivote
/// queda en la cara exterior, toda la pared falsa queda ADENTRO del muro y
/// no se ve nada. Para verificarlo: en Play, click derecho en el componente
/// > "Diagnosticar ubicacion de la pared falsa (Play)" (tambien corre solo al
/// arrancar Play en el Editor si "Check Placement In Editor" esta tildado).
///
/// Cablear el TeleportPoint de la pared ciega:
/// On Player Arrived -> WallCollapse.Collapse().
/// </summary>
public class WallCollapse : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("El FaintTransition de esta escena (Next Scene Name = Parte 5 - Backrooms, Color blanco-amarillo, Fade Out Duration ~1.2). Su Pre Delay tiene que cubrir grieta + derrumbe + luz: ~4.5.")]
    [SerializeField] private FaintTransition _faint;
    [Tooltip("Main Camera. Si se deja vacio, usa Camera.main.")]
    [SerializeField] private Transform _cameraTransform;
    [Tooltip("Material de los bloques: el de la pared del pasillo, para que la pared falsa se confunda con la real.")]
    [SerializeField] private Material _pieceMaterial;
    [Tooltip("Arrastrar Assets/Shaders/Abyss_Void.shader (se usa para el panel brillante, sin niebla).")]
    [SerializeField] private Shader _glowShader;

    [Header("Pared falsa (metros)")]
    [SerializeField] private float _width = 3f;
    [SerializeField] private float _height = 3f;
    [SerializeField] private int _columns = 5;
    [SerializeField] private int _rows = 5;
    [SerializeField] private float _thickness = 0.12f;
    [Tooltip("Separacion entre la cara VISIBLE de la pared real (donde va el pivote) y la cara trasera de los bloques. Tiene que ser MAYOR que lo que sobresalen zocalos/molduras/cornisa de la pared real, si no esas piezas tapan la pared falsa. En Parte 4 lo que mas sobresale es la cornisa (0.27 m): usar 0.4.")]
    [SerializeField] private float _gap = 0.3f;
    [Tooltip("Metros DETRAS de la cara trasera de los bloques donde va el panel brillante. Chico (0.05) para que el panel quede delante de zocalos/molduras y se vea por las juntas cuando los bloques se agrietan.")]
    [SerializeField] private float _panelInset = 0.05f;

    [Header("Textura de los bloques (opcional)")]
    [Tooltip("UV por metro sobre la pared (X = eje X local del pivote, flecha roja; Y = alto) para que el empapelado de los bloques CONTINUE el de la pared real, en vez de repetir la textura entera en cada bloque. (0,0) = UV comun del cubo. Parte 4 (medido del .glb): (0.07447, -0.11033).")]
    [SerializeField] private Vector2 _uvPerMeter = Vector2.zero;
    [Tooltip("UV que corresponde al pivote (abajo al centro de la pared falsa). Parte 4 con el pivote en (-136.31, 4.85, -202.04) y rotacion Y=90: (0.51984, 1.15433). Si se mueve el pivote en alto o de costado, el dibujo queda corrido y hay que recalcularlo.")]
    [SerializeField] private Vector2 _uvOffset = Vector2.zero;

    [Header("Luz")]
    [SerializeField] private Color _lightColor = new Color(1f, 0.85f, 0.45f);
    [Tooltip("Brillo HDR del panel detras de la pared (>1 hace reaccionar al Bloom).")]
    [SerializeField] private float _glowMax = 12f;
    [SerializeField] private float _lightMaxIntensity = 60f;
    [SerializeField] private float _lightMaxRange = 25f;
    [Tooltip("Metros DELANTE de la cara frontal de los bloques donde va la Light amarilla. Muy pegada a los bloques hace un punto quemado (y un halo de Bloom) justo en el centro de la vista.")]
    [SerializeField] private float _lightOffset = 0.5f;

    [Header("Tiempos")]
    [Tooltip("Temblor + grietas antes de que caigan los bloques.")]
    [SerializeField] private float _crackTime = 1.5f;
    [Tooltip("Lapso en el que van cayendo los bloques (cada uno arranca en un momento al azar dentro de este lapso).")]
    [SerializeField] private float _collapseSpread = 0.8f;
    [Tooltip("Cuanto tarda la luz en llegar al maximo.")]
    [SerializeField] private float _lightRampTime = 2.5f;
    [Tooltip("Metros que la luz 'arrastra' al jugador hacia la pared.")]
    [SerializeField] private float _pullDistance = 1.2f;
    [SerializeField] private float _shakeAmount = 0.03f;

    [Header("Sonido (opcional, 2D)")]
    [Tooltip("Crujido/derrumbe de madera.")]
    [SerializeField] private AudioClip _collapseClip;

    [Header("Diagnostico (solo Editor)")]
    [Tooltip("Al entrar a Play en el Editor, revisa que la pared falsa no quede adentro/detras de la geometria real y lo avisa en la Consola con los numeros para corregirlo. No corre en el celular.")]
    [SerializeField] private bool _checkPlacementInEditor = true;

    private readonly List<Transform> _pieces = new List<Transform>();
    private readonly List<Mesh> _meshes = new List<Mesh>();
    private Material _glowMat;
    private Light _light;
    private bool _triggered;

    private float PanelZ => Mathf.Max(0.01f, _gap - Mathf.Max(0.005f, _panelInset));

    private void Start()
    {
#if UNITY_EDITOR
        if (_checkPlacementInEditor)
        {
            CheckPlacement();
        }
#endif
        Build();
    }

    public void Collapse()
    {
        if (_triggered)
        {
            return;
        }
        _triggered = true;
        Debug.Log("[WallCollapse] La pared se desmorona.");

        if (_collapseClip != null)
        {
            AudioSource src = gameObject.AddComponent<AudioSource>();
            src.spatialBlend = 0f;
            src.PlayOneShot(_collapseClip);
        }

        if (_faint != null)
        {
            _faint.Trigger();
        }
        else
        {
            Debug.LogWarning("[WallCollapse] Falta asignar Faint: la pared cae pero no pasa de nivel.");
        }

        StartCoroutine(Sequence());
    }

    private IEnumerator Sequence()
    {
        Transform cam = _cameraTransform != null ? _cameraTransform : (Camera.main != null ? Camera.main.transform : null);
        Vector3 camStart = cam != null ? cam.position : Vector3.zero;
        Vector3 pullDir = -transform.forward; // hacia la pared
        float total = _crackTime + _collapseSpread + _lightRampTime + 2f;
        float t = 0f;

        // Rotacion de "grieta" de cada bloque, y momento en que empieza a caer.
        var crackRot = new Quaternion[_pieces.Count];
        var startRot = new Quaternion[_pieces.Count];
        var fallAt = new float[_pieces.Count];
        var velocity = new Vector3[_pieces.Count];
        var spin = new Vector3[_pieces.Count];
        for (int i = 0; i < _pieces.Count; i++)
        {
            startRot[i] = _pieces[i].localRotation;
            crackRot[i] = Quaternion.Euler(Random.Range(-4f, 4f), Random.Range(-4f, 4f), Random.Range(-6f, 6f));
            fallAt[i] = _crackTime + Random.Range(0f, _collapseSpread);
            velocity[i] = transform.forward * Random.Range(0.5f, 1.8f); // se vuelcan hacia el pasillo
            spin[i] = new Vector3(Random.Range(60f, 160f), Random.Range(-40f, 40f), Random.Range(-60f, 60f));
        }
        float floorY = transform.position.y;

        while (t < total)
        {
            t += Time.deltaTime;

            // 1. Grietas: los bloques se tuercen un poco y se cuela luz por las juntas.
            float crackK = Mathf.Clamp01(t / _crackTime);
            // 2. La luz crece desde que empiezan las grietas hasta el final.
            float lightK = Mathf.Clamp01(t / (_crackTime + _collapseSpread + _lightRampTime));
            SetGlow(Mathf.Lerp(0.3f, _glowMax, lightK * lightK));
            if (_light != null)
            {
                _light.intensity = Mathf.Lerp(0f, _lightMaxIntensity, lightK * lightK);
                _light.range = Mathf.Lerp(3f, _lightMaxRange, lightK);
            }

            for (int i = 0; i < _pieces.Count; i++)
            {
                Transform p = _pieces[i];
                if (p == null)
                {
                    continue;
                }
                if (t < fallAt[i])
                {
                    p.localRotation = Quaternion.Slerp(startRot[i], startRot[i] * crackRot[i], crackK);
                    continue;
                }
                // 3. Derrumbe: gravedad + vuelco hacia el pasillo, hasta el piso.
                if (p.position.y > floorY + _thickness * 0.5f)
                {
                    velocity[i] += Vector3.down * 9.8f * Time.deltaTime;
                    p.position += velocity[i] * Time.deltaTime;
                    p.Rotate(spin[i] * Time.deltaTime, Space.Self);
                }
            }

            // 4. Temblor + la luz "arrastra" al jugador hacia la pared.
            if (cam != null)
            {
                float shake = _shakeAmount * (1f - Mathf.Clamp01((t - _crackTime - _collapseSpread) / 1f));
                Vector3 noise = new Vector3(
                    Mathf.PerlinNoise(t * 25f, 0f) - 0.5f,
                    Mathf.PerlinNoise(0f, t * 25f) - 0.5f,
                    0f) * 2f * shake;
                float pullK = Mathf.Clamp01((t - _crackTime) / (_collapseSpread + _lightRampTime));
                cam.position = camStart + pullDir * (_pullDistance * pullK * pullK) + noise;
            }

            yield return null;
        }
    }

    // ---------------- Armado ----------------

    private void Build()
    {
        if (_glowShader != null)
        {
            GameObject glow = GameObject.CreatePrimitive(PrimitiveType.Quad);
            glow.name = "Luz detras de la pared";
            Destroy(glow.GetComponent<Collider>());
            glow.transform.SetParent(transform, false);
            // Justo detras de los bloques (y delante de zocalos/molduras de la pared real).
            glow.transform.localPosition = new Vector3(0f, _height / 2f, PanelZ);
            glow.transform.localScale = new Vector3(_width * 0.98f, _height * 0.98f, 1f);
            _glowMat = new Material(_glowShader);
            MeshRenderer glowRenderer = glow.GetComponent<MeshRenderer>();
            glowRenderer.sharedMaterial = _glowMat;
            glowRenderer.shadowCastingMode = ShadowCastingMode.Off;
            glowRenderer.receiveShadows = false;
            SetGlow(0.3f);
        }
        else
        {
            Debug.LogWarning("[WallCollapse] Falta asignar Glow Shader (Assets/Shaders/Abyss_Void.shader): no hay panel brillante detras de la pared.");
        }

        GameObject lightGo = new GameObject("Luz amarilla");
        lightGo.transform.SetParent(transform, false);
        lightGo.transform.localPosition = new Vector3(0f, _height / 2f, _gap + _thickness + Mathf.Max(0f, _lightOffset));
        _light = lightGo.AddComponent<Light>();
        _light.type = LightType.Point;
        _light.color = _lightColor;
        _light.intensity = 0f;
        _light.shadows = LightShadows.None;
        // En el celular (Mobile_Renderer, Forward) cada objeto recibe como maximo
        // 4 luces adicionales, y la pared del pasillo es UNA malla para todo el
        // edificio: "Important" le da prioridad a esta luz frente a las del pasillo.
        _light.renderMode = LightRenderMode.ForcePixel;

        if (_pieceMaterial == null)
        {
            Debug.LogWarning("[WallCollapse] Falta asignar Piece Material: no se arma la pared falsa.");
            return;
        }
        float cw = _width / Mathf.Max(1, _columns);
        float rh = _height / Mathf.Max(1, _rows);
        for (int r = 0; r < _rows; r++)
        {
            for (int c = 0; c < _columns; c++)
            {
                GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
                piece.name = "Bloque " + r + "-" + c;
                Destroy(piece.GetComponent<Collider>()); // que no frene la mirada
                piece.transform.SetParent(transform, false);
                piece.transform.localPosition = new Vector3(
                    -_width / 2f + cw * (c + 0.5f),
                    rh * (r + 0.5f),
                    _gap + _thickness / 2f);
                piece.transform.localScale = new Vector3(cw, rh, _thickness);
                MeshRenderer pieceRenderer = piece.GetComponent<MeshRenderer>();
                pieceRenderer.sharedMaterial = _pieceMaterial;
                pieceRenderer.shadowCastingMode = ShadowCastingMode.Off;
                if (_uvPerMeter != Vector2.zero)
                {
                    ApplyWallUVs(piece.GetComponent<MeshFilter>(), piece.transform.localPosition, piece.transform.localScale);
                }
                _pieces.Add(piece.transform);
            }
        }
    }

    // Proyeccion plana (de frente) de las coordenadas del bloque sobre la pared:
    // mismo UV por metro que la pared real, asi el dibujo sigue de un bloque a
    // otro. Los costados de 12 cm quedan estirados, no se notan.
    private void ApplyWallUVs(MeshFilter filter, Vector3 center, Vector3 size)
    {
        if (filter == null)
        {
            return;
        }
        Mesh mesh = filter.mesh; // copia propia de este bloque
        Vector3[] verts = mesh.vertices;
        var uvs = new Vector2[verts.Length];
        for (int i = 0; i < verts.Length; i++)
        {
            float lx = center.x + verts[i].x * size.x;
            float ly = center.y + verts[i].y * size.y;
            uvs[i] = new Vector2(_uvOffset.x + _uvPerMeter.x * lx, _uvOffset.y + _uvPerMeter.y * ly);
        }
        mesh.uv = uvs;
        _meshes.Add(mesh);
    }

    private void OnDestroy()
    {
        foreach (Mesh mesh in _meshes)
        {
            if (mesh != null)
            {
                Destroy(mesh);
            }
        }
        if (_glowMat != null)
        {
            Destroy(_glowMat);
        }
    }

    private void SetGlow(float intensity)
    {
        if (_glowMat != null)
        {
            _glowMat.SetColor("_Color", _lightColor * intensity);
        }
    }

    // ---------------- Diagnostico (solo Editor, en Play) ----------------

    /// <summary>
    /// Mide, con rayos contra la geometria REAL de la escena, donde queda la
    /// cara visible de la pared respecto de este pivote, y avisa en la Consola
    /// si los bloques o el panel quedan adentro/detras de ella (el bug de
    /// "la pared falsa no se ve"). Agrega MeshColliders temporales a las mallas
    /// cercanas (los modelos glTF no traen colliders) y los borra enseguida;
    /// activa Physics.queriesHitBackfaces para no saltearse caras dobles.
    /// Solo en Play dentro del Editor. Se puede llamar desde Unity MCP:
    /// Object.FindFirstObjectByType&lt;WallCollapse&gt;().CheckPlacement();
    /// </summary>
    [ContextMenu("Diagnosticar ubicacion de la pared falsa (Play)")]
    public void CheckPlacement()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[WallCollapse] El diagnostico agrega MeshColliders temporales: correrlo en Play.");
            return;
        }

        float halfW = _width * 0.5f;
        float blocksBack = _gap;
        float blocksFront = _gap + _thickness;
        float panelZ = PanelZ;
        const float probeBehind = 1.5f;          // metros detras del pivote (adentro del muro)
        float probeFront = blocksFront + 2f;     // los rayos arrancan 2 m adentro del pasillo

        // Caja de busqueda (en mundo) que cubre el area de la pared falsa.
        Bounds probe = new Bounds(transform.TransformPoint(new Vector3(-halfW, 0f, -probeBehind)), Vector3.zero);
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = new Vector3(
                (i & 1) == 0 ? -halfW : halfW,
                (i & 2) == 0 ? 0f : _height,
                (i & 4) == 0 ? -probeBehind : probeFront);
            probe.Encapsulate(transform.TransformPoint(corner));
        }

        var temp = new List<MeshCollider>();
        foreach (MeshRenderer mr in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            if (!mr.enabled || mr.transform.IsChildOf(transform) || !mr.bounds.Intersects(probe))
            {
                continue;
            }
            MeshFilter mf = mr.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null)
            {
                continue;
            }
            MeshCollider mc = mr.gameObject.AddComponent<MeshCollider>();
            mc.sharedMesh = mf.sharedMesh;
            temp.Add(mc);
        }
        var tempSet = new HashSet<Collider>(temp);

        bool prevBackfaces = Physics.queriesHitBackfaces;
        Physics.queriesHitBackfaces = true;
        Physics.SyncTransforms();

        // Para cada punto de una grilla sobre la pared falsa: la superficie REAL
        // mas cercana viniendo desde el pasillo (z local, + = hacia el pasillo).
        const int cols = 13;
        const int rows = 121; // paso ~0.1 m en alto: zocalos, molduras y cornisas son franjas finas
        var hitZ = new List<float>();
        var hitName = new List<string>();
        int misses = 0;
        Vector3 dir = -transform.forward;
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                float lx = Mathf.Lerp(-halfW * 0.98f, halfW * 0.98f, c / (float)(cols - 1));
                float ly = Mathf.Lerp(_height * 0.01f, _height * 0.99f, r / (float)(rows - 1));
                Vector3 origin = transform.TransformPoint(new Vector3(lx, ly, probeFront));
                float maxDist = Vector3.Distance(origin, transform.TransformPoint(new Vector3(lx, ly, -probeBehind)));

                float best = float.PositiveInfinity;
                RaycastHit bestHit = default;
                foreach (RaycastHit h in Physics.RaycastAll(origin, dir, maxDist, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (tempSet.Contains(h.collider) && h.distance < best)
                    {
                        best = h.distance;
                        bestHit = h;
                    }
                }
                if (float.IsPositiveInfinity(best))
                {
                    misses++;
                    continue;
                }
                hitZ.Add(transform.InverseTransformPoint(bestHit.point).z);
                hitName.Add(bestHit.collider.name);
            }
        }

        Physics.queriesHitBackfaces = prevBackfaces;
        foreach (MeshCollider mc in temp)
        {
            if (mc != null)
            {
                DestroyImmediate(mc);
            }
        }
        Physics.SyncTransforms();

        if (hitZ.Count == 0)
        {
            Debug.LogWarning("[WallCollapse] Diagnostico: ningun rayo encontro pared real en el area de la pared falsa. " +
                             "El pivote esta sobre la pared ciega y la flecha azul (Z) apunta al pasillo?");
            return;
        }

        // Cara de la pared = mediana (la mayoria de los puntos pegan en la pared plana).
        var sorted = new List<float>(hitZ);
        sorted.Sort();
        float face = sorted[sorted.Count / 2];

        // Lo que sobresale de la pared (zocalo, molduras, cornisa: hasta 0.5 m),
        // cuantos puntos de los bloques quedan tapados, y objetos sueltos delante.
        float maxNear = face;
        string maxNearName = "-";
        int hidden = 0;
        string hiddenBy = null;
        int frontCount = 0;
        string frontName = null;
        for (int i = 0; i < hitZ.Count; i++)
        {
            float z = hitZ[i];
            if (z > face + 0.5f)
            {
                // Objeto suelto en el pasillo (mueble, moldura de la pared lateral):
                // se avisa aparte, no cuenta como "pared falsa adentro del muro".
                frontCount++;
                if (frontName == null)
                {
                    frontName = hitName[i];
                }
                continue;
            }
            if (z >= blocksBack - 0.005f)
            {
                hidden++;
                if (hiddenBy == null)
                {
                    hiddenBy = hitName[i];
                }
            }
            if (z > maxNear)
            {
                maxNear = z;
                maxNearName = hitName[i];
            }
        }
        float protrusion = maxNear - face;
        string nums = string.Format(
            "(z = metros desde el pivote hacia el pasillo) cara visible de la pared z={0:+0.000;-0.000}, lo que mas sobresale z={1:+0.000;-0.000} ('{2}', {3:0.000} m sobre la pared), " +
            "panel z={4:0.000}, bloques z={5:0.000}..{6:0.000}, puntos tapados {7}/{8}, rayos sin pared {9}.",
            face, maxNear, maxNearName, protrusion, panelZ, blocksBack, blocksFront, hidden, hitZ.Count, misses);

        if (hidden * 2 > hitZ.Count)
        {
            Debug.LogError("[WallCollapse] La pared falsa queda ADENTRO/DETRAS de la geometria real ('" + hiddenBy + "'): por eso no se ve. " + nums +
                           string.Format(" Arreglo: mover el pivote {0:0.00} m en la direccion de la flecha azul (hasta la cara visible) y dejar Gap >= {1:0.00}.",
                               face, protrusion + 0.1f));
        }
        else if (hidden > 0)
        {
            Debug.LogWarning("[WallCollapse] Parte de la pared falsa queda tapada por '" + hiddenBy + "'. " + nums +
                             string.Format(" Subir Gap a {0:0.00}.", Mathf.Max(_gap, maxNear) + 0.1f));
        }
        else if (maxNear >= panelZ - 0.005f)
        {
            Debug.LogWarning("[WallCollapse] Los bloques se ven, pero el panel brillante queda detras de '" + maxNearName +
                             "' en algunas zonas (se va a ver cortado al derrumbarse). " + nums +
                             string.Format(" Subir Gap a {0:0.00} o bajar Panel Inset.", maxNear + _panelInset + 0.05f));
        }
        else
        {
            Debug.Log("[WallCollapse] Ubicacion OK. " + nums);
        }

        if (face < -0.05f)
        {
            Debug.LogWarning(string.Format("[WallCollapse] El pivote quedo {0:0.00} m DELANTE de la pared: la pared falsa se ve flotando separada. Moverlo {0:0.00} m hacia la pared (contra la flecha azul).", -face));
        }
        if (frontCount > 0)
        {
            Debug.Log(string.Format("[WallCollapse] (info) Hay geometria suelta delante de la pared falsa ('{0}', {1} puntos, a mas de 0.5 m): tapa un poco el borde desde algunos angulos, es normal.",
                frontName, frontCount));
        }
#else
        Debug.Log("[WallCollapse] CheckPlacement solo funciona en el Editor.");
#endif
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.9f);
        Gizmos.DrawWireCube(new Vector3(0f, _height / 2f, _gap + _thickness / 2f), new Vector3(_width, _height, _thickness));
        // Panel brillante.
        Gizmos.color = new Color(1f, 1f, 0.6f, 0.6f);
        Gizmos.DrawWireCube(new Vector3(0f, _height / 2f, PanelZ), new Vector3(_width * 0.98f, _height * 0.98f, 0.001f));
        // Plano del pivote = debe coincidir con la cara VISIBLE de la pared real.
        Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.9f);
        Gizmos.DrawWireCube(new Vector3(0f, _height / 2f, 0f), new Vector3(_width, _height, 0.001f));
        Gizmos.DrawLine(Vector3.zero, Vector3.forward * 1f); // hacia el pasillo
    }
}
