using System.Collections;
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
    [Tooltip("Pivote de la puerta que se abre al Empezar. Si se deja vacio (junto con Camera Rig), Empezar carga directo con fundido.")]
    [SerializeField] private Transform _door;

    [Tooltip("Rotacion local de la puerta CERRADA.")]
    [SerializeField] private Vector3 _doorClosedLocalEuler = Vector3.zero;

    [Tooltip("Rotacion local de la puerta ABIERTA.")]
    [SerializeField] private Vector3 _doorOpenLocalEuler = new Vector3(0f, 100f, 0f);

    [Tooltip("Segundos que tarda en abrirse la puerta.")]
    [SerializeField] private float _doorOpenDuration = 1.1f;

    [Tooltip("Transform que avanza hacia/por la puerta (la RAIZ del objeto Player). Si tiene PlayerFollowsCamera, se desactiva durante el avance.")]
    [SerializeField] private Transform _cameraRig;

    [Tooltip("Metros que avanza la camara cruzando la puerta.")]
    [SerializeField] private float _walkDistance = 3.8f;

    [Tooltip("Segundos que dura el avance.")]
    [SerializeField] private float _walkDuration = 2f;

    [Tooltip("Segundos minimos en negro 'cargando' antes de aparecer en el Tutorial.")]
    [SerializeField] private float _minLoadSeconds = 2.5f;

    [Tooltip("Opcional: objeto de texto 'CARGANDO...' que se prende durante la carga (encima del fundido).")]
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

        // Direccion de avance: la del marco de la puerta (su padre, que NO rota
        // al abrirse), tomada ANTES de abrir. Asi se entra derecho aunque estes
        // mirando para otro lado al elegir Empezar.
        Transform dirSource = _door.parent != null ? _door.parent : _door;
        Vector3 dir = dirSource.forward;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
        dir.Normalize();

        // 1. Abrir la puerta.
        yield return RotateLocal(_door, _doorClosedLocalEuler, _doorOpenLocalEuler, _doorOpenDuration);

        // Respiro corto una vez abierta.
        yield return new WaitForSeconds(0.15f);

        // 2. Avanzar cruzando la puerta.
        Vector3 from = _cameraRig.position;
        Vector3 to = from + dir * _walkDistance;
        yield return MovePosition(_cameraRig, from, to, _walkDuration);

        // 3. Fundido a negro (se queda en negro), "cargando" unos segundos, y
        //    recien ahi aparecer en el Tutorial.
        if (_fade != null)
        {
            yield return StartCoroutine(_fade.FadeToBlack());
        }
        if (_loadingText != null)
        {
            _loadingText.SetActive(true);
        }

        AsyncOperation op = SceneManager.LoadSceneAsync(_gameSceneName);
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

    private static IEnumerator RotateLocal(Transform t, Vector3 fromEuler, Vector3 toEuler, float duration)
    {
        Quaternion a = Quaternion.Euler(fromEuler);
        Quaternion b = Quaternion.Euler(toEuler);
        t.localRotation = a;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            t.localRotation = Quaternion.Slerp(a, b, k);
            yield return null;
        }
        t.localRotation = b;
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
