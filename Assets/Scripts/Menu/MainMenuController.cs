using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Controla la escena "Menu Principal": muestra un panel por vez (principal,
/// configuracion, creditos) y, al elegir Empezar, corre la secuencia de entrada
/// (abrir la puerta + avanzar + fundido) antes de cargar la escena de juego.
///
/// Los botones son objetos InteractiveObject comunes. Cablear su OnSelected a:
///   - Boton Empezar        -> StartGame()
///   - Boton Configuracion  -> OpenConfig()
///   - Boton Creditos       -> OpenCredits()
///   - Botones Volver        -> OpenMain()
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Header("Paneles (se activa uno por vez)")]
    [SerializeField] private GameObject _mainPanel;
    [SerializeField] private GameObject _configPanel;
    [SerializeField] private GameObject _creditsPanel;

    [Header("Empezar a jugar")]
    [Tooltip("Nombre EXACTO de la escena que carga el boton Empezar. Tiene que estar en Build Settings.")]
    [SerializeField] private string _gameSceneName = "Tutorial";

    [Tooltip("Fundido a negro. Se usa al final de la secuencia de entrada (o directo si no hay secuencia).")]
    [SerializeField] private VRFadeController _fade;

    [Header("Secuencia de entrada (opcional)")]
    [Tooltip("Objeto de la puerta que se abre al Empezar (el del modelo). Si se deja vacio (junto con Camera Rig), Empezar carga directo con fundido.")]
    [SerializeField] private Transform _door;

    [Tooltip("Grados que gira la puerta al abrirse, alrededor de la vertical. Negativo = abre para el otro lado.")]
    [SerializeField] private float _doorOpenAngle = 100f;

    [Tooltip("Segundos que tarda en abrirse la puerta.")]
    [SerializeField] private float _doorOpenDuration = 1.4f;

    [Tooltip("Transform que avanza hacia/por la puerta (la RAIZ del objeto Player). Si tiene PlayerFollowsCamera, se desactiva durante el avance.")]
    [SerializeField] private Transform _cameraRig;

    [Tooltip("Punto por el que el jugador cruza (centro de la puerta). Si esta asignado, el avance va hacia aca.")]
    [SerializeField] private Transform _walkThroughPoint;

    [Tooltip("Metros que sigue avanzando DESPUES de llegar a la puerta.")]
    [SerializeField] private float _extraWalkPastDoor = 1.5f;

    [Tooltip("Metros de avance si no hay Walk Through Point (respaldo).")]
    [SerializeField] private float _walkDistance = 3.8f;

    [Tooltip("Segundos que dura el avance.")]
    [SerializeField] private float _walkDuration = 2f;

    [Tooltip("Segundos minimos en negro con el cartel del nivel antes de aparecer en el juego.")]
    [SerializeField] private float _minLoadSeconds = 3f;

    [Tooltip("Nombre del nivel que se muestra GRANDE en la pantalla de carga (ej: TUTORIAL).")]
    [SerializeField] private string _levelDisplayName = "TUTORIAL";

    [Tooltip("Opcional: objeto de texto que se prende durante la carga (encima del fundido). Se le setea '<nivel> / cargando...'.")]
    [SerializeField] private GameObject _loadingText;

    // Evita que mirar Empezar dos veces (el gaze re-selecciona) dispare la
    // secuencia dos veces.
    private bool _isLoading;

    private void Start()
    {
        OpenMain();
    }

    public void OpenMain()
    {
        ShowOnly(_mainPanel);
    }

    public void OpenConfig()
    {
        ShowOnly(_configPanel);
    }

    public void OpenCredits()
    {
        ShowOnly(_creditsPanel);
    }

    public void StartGame()
    {
        if (_isLoading)
        {
            return;
        }

        // Chequeo ANTES de abrir la puerta y fundir a negro: si el nombre no
        // coincide con Build Settings, mejor que Empezar no haga nada (con un
        // error claro en la consola / adb logcat) a quedar en negro para siempre.
        if (string.IsNullOrEmpty(_gameSceneName) || !Application.CanStreamedLevelBeLoaded(_gameSceneName))
        {
            Debug.LogError("[MainMenuController] La escena '" + _gameSceneName + "' no esta agregada/tildada en File > Build Settings (o el nombre no coincide exacto). Empezar no hace nada.");
            return;
        }
        _isLoading = true;

        if (_door != null && _cameraRig != null)
        {
            StartCoroutine(EntranceSequence());
        }
        else if (_fade != null)
        {
            StartCoroutine(LoadWithFadeOnly());
        }
        else
        {
            SceneManager.LoadScene(_gameSceneName);
        }
    }

    private IEnumerator EntranceSequence()
    {
        // El menu se apaga para no atravesarlo con la cara al avanzar.
        if (_mainPanel != null) _mainPanel.SetActive(false);
        if (_configPanel != null) _configPanel.SetActive(false);
        if (_creditsPanel != null) _creditsPanel.SetActive(false);

        // PlayerFollowsCamera pelearia con el movimiento del rig; se apaga (la
        // escena se descarga enseguida, no hace falta restaurarlo).
        PlayerFollowsCamera follow = _cameraRig.GetComponent<PlayerFollowsCamera>();
        if (follow != null)
        {
            follow.enabled = false;
        }

        // Distancia y direccion CAMARA -> centro de la puerta, horizontal,
        // calculadas ANTES de abrir (rotar la puerta no mueve su posicion).
        Transform camT = Camera.main != null ? Camera.main.transform : _cameraRig;
        Vector3 camStart = camT.position;
        Vector3 aim = _walkThroughPoint != null ? _walkThroughPoint.position
                    : (_door != null ? _door.position : camStart + camT.forward);
        Vector3 flat = aim - camStart;
        flat.y = 0f;
        if (flat.sqrMagnitude < 0.0001f) flat = new Vector3(camT.forward.x, 0f, camT.forward.z);
        if (flat.sqrMagnitude < 0.0001f) flat = Vector3.forward;
        float camToDoor = flat.magnitude;
        Vector3 dir = flat / camToDoor;

        // 1. Abrir la puerta: girar _door (el pivote "PuertaMenu", ya ubicado en
        //    la bisagra por el generador) _doorOpenAngle grados sobre la vertical.
        Debug.Log("[MainMenuController] Abriendo puerta '" + _door.name + "' pos=" +
                  _door.position.ToString("0.00") + " angulo=" + _doorOpenAngle);
        yield return SwingAround(_door, _door.position, Vector3.up, _doorOpenAngle, _doorOpenDuration);

        // Respiro corto una vez abierta.
        yield return new WaitForSeconds(0.2f);

        // 2. Avanzar: se mueve la RAIZ del rig, pero la distancia se mide sobre
        //    la CAMARA (la camara viaja camToDoor + _extraWalkPastDoor y cruza).
        float walk = _walkThroughPoint != null ? camToDoor + _extraWalkPastDoor : _walkDistance;
        Vector3 rigStart = _cameraRig.position;
        Vector3 rigEnd = rigStart + dir * walk;
        yield return MovePosition(_cameraRig, rigStart, rigEnd, _walkDuration);

        // 3. Fundido a negro (se queda en negro), "cargando" unos segundos, y
        //    recien ahi aparecer en el Tutorial.
        if (_fade != null)
        {
            yield return StartCoroutine(_fade.FadeToBlack());
        }
        if (_loadingText != null)
        {
            TMP_Text label = _loadingText.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.richText = true;
                label.text = "<size=160%><b>" + _levelDisplayName + "</b></size>\n<size=55%>cargando...</size>";
            }
            _loadingText.SetActive(true);
        }

        AsyncOperation op = SceneManager.LoadSceneAsync(_gameSceneName);
        if (op == null)
        {
            Debug.LogError("[MainMenuController] No pude cargar '" + _gameSceneName + "'. Esta en File > Build Settings?");
            yield break;
        }
        op.allowSceneActivation = false;

        float t = 0f;
        // Espera el minimo pedido Y a que la escena este lista (con activation
        // desactivada, progress se clava en 0.9 cuando termino de cargar).
        while (t < _minLoadSeconds || op.progress < 0.9f)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        op.allowSceneActivation = true;
    }

    private IEnumerator LoadWithFadeOnly()
    {
        yield return _fade.FadeOutAndIn(() => SceneManager.LoadScene(_gameSceneName));
    }

    // Gira 't' 'angle' grados alrededor del eje 'axis' que pasa por 'pivot'
    // (mundo), sin reparentar: reposiciona y rota en cada frame.
    private static IEnumerator SwingAround(Transform t, Vector3 pivot, Vector3 axis, float angle, float duration)
    {
        Quaternion startRot = t.rotation;
        Vector3 startPos = t.position;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            Quaternion r = Quaternion.AngleAxis(angle * k, axis);
            t.rotation = r * startRot;
            t.position = pivot + r * (startPos - pivot);
            yield return null;
        }

        Quaternion rf = Quaternion.AngleAxis(angle, axis);
        t.rotation = rf * startRot;
        t.position = pivot + rf * (startPos - pivot);
    }

    private static IEnumerator MovePosition(Transform t, Vector3 from, Vector3 to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            t.position = Vector3.Lerp(from, to, k);
            yield return null;
        }
        t.position = to;
    }

    private void ShowOnly(GameObject panel)
    {
        if (_mainPanel != null)
        {
            _mainPanel.SetActive(panel == _mainPanel);
        }
        if (_configPanel != null)
        {
            _configPanel.SetActive(panel == _configPanel);
        }
        if (_creditsPanel != null)
        {
            _creditsPanel.SetActive(panel == _creditsPanel);
        }
    }
}
