using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Etapa 7: fundido a negro para tapar el "salto" del teletransporte y
/// reducir la desorientacion. Necesita una Image negra a pantalla completa
/// (Canvas World Space, hijo de la camara) que arranca en alpha 0.
/// </summary>
public class VRFadeController : MonoBehaviour
{
    [Tooltip("Imagen negra a pantalla completa. Debe arrancar con alpha 0.")]
    [SerializeField] private Image _fadeImage;

    [Tooltip("Duracion en segundos de cada mitad del fundido (ida y vuelta).")]
    [SerializeField] private float _fadeDuration = 0.3f;

    public bool IsFading { get; private set; }

    /// <summary>
    /// Funde a negro, ejecuta onBlackout (por ejemplo, mover al jugador) una
    /// vez que la pantalla esta completamente negra, y despues funde de
    /// vuelta a la escena. Se usa con StartCoroutine.
    /// </summary>
    public IEnumerator FadeOutAndIn(Action onBlackout)
    {
        IsFading = true;
        yield return Fade(0f, 1f);
        onBlackout?.Invoke();
        yield return Fade(1f, 0f);
        IsFading = false;
    }

    private IEnumerator Fade(float from, float to)
    {
        if (_fadeImage == null)
        {
            yield break;
        }

        float elapsed = 0f;
        Color baseColor = _fadeImage.color;

        while (elapsed < _fadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(from, to, elapsed / _fadeDuration);
            _fadeImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
            yield return null;
        }

        _fadeImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, to);
    }
}
