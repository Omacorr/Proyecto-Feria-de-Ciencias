using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Reproduce una locucion (una voz explicando el nivel) UNA sola vez al empezar
/// la escena. Pensado para el Tutorial: apenas aparecas, se escucha como jugarlo.
///
/// Poner este componente en un GameObject de la escena, asignar el clip de voz
/// y, opcionalmente, un texto de subtitulo (TMP) que se muestra mientras suena.
/// No se repite dentro de la misma carga de escena; si volves a entrar a la
/// escena, vuelve a sonar desde el principio.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class SceneNarration : MonoBehaviour
{
    [Tooltip("Clip de voz que explica el nivel.")]
    [SerializeField] private AudioClip _voiceClip;

    [Tooltip("Segundos de espera antes de arrancar la locucion (para que termines de aparecer / el fundido abra).")]
    [SerializeField] private float _delay = 1.5f;

    [Range(0f, 1f)]
    [Tooltip("Volumen de la voz.")]
    [SerializeField] private float _volume = 1f;

    [Header("Subtitulo (opcional)")]
    [Tooltip("Texto TMP donde se muestra el subtitulo mientras suena la voz. Si se deja vacio, no hay subtitulo.")]
    [SerializeField] private TMP_Text _subtitleLabel;

    [TextArea(2, 6)]
    [Tooltip("Texto del subtitulo.")]
    [SerializeField] private string _subtitleText;

    private AudioSource _source;

    private void Awake()
    {
        _source = GetComponent<AudioSource>();
        _source.playOnAwake = false;
        _source.loop = false;
        _source.spatialBlend = 0f; // 2D: se escucha parejo, no depende de a donde mires

        if (_subtitleLabel != null)
        {
            _subtitleLabel.gameObject.SetActive(false);
        }
    }

    private void Start()
    {
        if (_voiceClip == null)
        {
            Debug.LogWarning("[SceneNarration] No hay clip de voz asignado en " + gameObject.name + ".");
            return;
        }
        StartCoroutine(PlayRoutine());
    }

    private IEnumerator PlayRoutine()
    {
        if (_delay > 0f)
        {
            yield return new WaitForSeconds(_delay);
        }

        _source.clip = _voiceClip;
        _source.volume = _volume;
        _source.Play();

        bool showSubtitle = _subtitleLabel != null && !string.IsNullOrEmpty(_subtitleText);
        if (showSubtitle)
        {
            _subtitleLabel.text = _subtitleText;
            _subtitleLabel.gameObject.SetActive(true);
        }

        yield return new WaitWhile(() => _source != null && _source.isPlaying);

        if (_subtitleLabel != null)
        {
            _subtitleLabel.gameObject.SetActive(false);
        }
    }
}
