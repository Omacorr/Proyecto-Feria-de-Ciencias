using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Mensaje corto en pantalla para VR ("Esta cerrada. Necesito una llave.").
/// Va en un GameObject con un TextMeshPro (3D, NO UGUI) hijo de la camara,
/// con el material "SpecialElite SDF Overlay" (ver gotcha #26 de CLAUDE.md)
/// para que la geometria cercana no lo tape.
///
/// Arranca invisible. Show(texto) lo hace aparecer con fundido, lo deja un
/// rato y lo funde hacia afuera. Si se llama de nuevo con el MISMO texto
/// mientras ya esta visible no se reinicia (GazeController re-dispara
/// OnGazeSelect cada ~2 s mientras se sigue mirando el objeto, y sin esto el
/// mensaje quedaria parpadeando o pegado para siempre). Usa tiempo no
/// escalado, asi que funciona aunque Time.timeScale sea distinto de 1.
/// </summary>
public class VRHintText : MonoBehaviour
{
    [Tooltip("El TextMeshPro (3D) a mostrar. Si queda vacio se busca en este mismo objeto.")]
    [SerializeField] private TMP_Text _text;

    [Tooltip("Segundos que el mensaje queda totalmente visible.")]
    [SerializeField] private float _visibleSeconds = 2.5f;

    [Tooltip("Segundos del fundido de entrada.")]
    [SerializeField] private float _fadeInSeconds = 0.25f;

    [Tooltip("Segundos del fundido de salida.")]
    [SerializeField] private float _fadeOutSeconds = 0.5f;

    private Coroutine _routine;
    private string _currentText;
    private float _endTime;

    private void Awake()
    {
        if (_text == null)
        {
            _text = GetComponent<TMP_Text>();
        }

        SetAlpha(0f);
    }

    private void OnDisable()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }

        _currentText = null;
        SetAlpha(0f);
    }

    /// <summary>
    /// Muestra el texto con fundido. Si ya se esta mostrando exactamente ese
    /// texto, solo extiende el tiempo visible (no reinicia el fundido).
    /// </summary>
    public void Show(string text)
    {
        if (_text == null)
        {
            _text = GetComponent<TMP_Text>();
            if (_text == null)
            {
                Debug.LogWarning($"[VRHintText] {gameObject.name}: no hay TMP_Text para mostrar '{text}'.");
                return;
            }
        }

        if (!isActiveAndEnabled)
        {
            return;
        }

        // Mismo texto ya en pantalla: alargamos la estadia sin reiniciar.
        if (_routine != null && text == _currentText)
        {
            _endTime = Time.unscaledTime + _visibleSeconds;
            return;
        }

        if (_routine != null)
        {
            StopCoroutine(_routine);
        }

        _currentText = text;
        _text.text = text;
        _routine = StartCoroutine(Run());
    }

    /// <summary>Oculta el mensaje de inmediato.</summary>
    public void HideNow()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }

        _currentText = null;
        SetAlpha(0f);
    }

    private IEnumerator Run()
    {
        _endTime = Time.unscaledTime + _visibleSeconds;

        float t = 0f;
        while (t < _fadeInSeconds)
        {
            t += Time.unscaledDeltaTime;
            SetAlpha(Mathf.Clamp01(t / Mathf.Max(0.0001f, _fadeInSeconds)));
            yield return null;
        }

        SetAlpha(1f);

        // _endTime puede extenderse desde Show() mientras esperamos.
        while (Time.unscaledTime < _endTime)
        {
            yield return null;
        }

        t = 0f;
        while (t < _fadeOutSeconds)
        {
            t += Time.unscaledDeltaTime;
            SetAlpha(1f - Mathf.Clamp01(t / Mathf.Max(0.0001f, _fadeOutSeconds)));
            yield return null;
        }

        SetAlpha(0f);
        _currentText = null;
        _routine = null;
    }

    private void SetAlpha(float a)
    {
        if (_text == null)
        {
            return;
        }

        Color c = _text.color;
        c.a = a;
        _text.color = c;
    }
}
