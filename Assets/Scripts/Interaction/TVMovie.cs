using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Video;

/// <summary>
/// Cumpleaños (Parte 2): la "mini pelicula" de la tele. Play() la arranca (se
/// cablea desde VCRSlot._onTapeInserted). Cuando termina, dispara _onMovieEnd
/// UNA vez -> ahi se cablea MonsterChaser.StartChase() (y lo que haga falta:
/// apagar la peli, prender luces, etc.).
///
/// Dos modos:
///  - Con VideoPlayer: reproduce el clip y espera a loopPointReached (fin real).
///  - Con timer (_useTimerInsteadOfVideo): no hace falta un video, solo espera
///    _movieDuration segundos. Sirve para probar el flujo sin tener el .mp4.
/// </summary>
public class TVMovie : MonoBehaviour
{
    [Header("Video (opcional)")]
    [Tooltip("VideoPlayer que renderiza a la pantalla de la tele. Si se deja vacio, se usa el timer.")]
    [SerializeField] private VideoPlayer _videoPlayer;

    [Tooltip("Tildado: ignora el VideoPlayer y solo espera Movie Duration segundos.")]
    [SerializeField] private bool _useTimerInsteadOfVideo = true;

    [Tooltip("Segundos que 'dura' la pelicula si se usa el timer.")]
    [SerializeField] private float _movieDuration = 20f;

    [Header("Visual (opcional)")]
    [Tooltip("Objeto que representa la pantalla PRENDIDA (glow/emisivo). Se prende al empezar y se apaga al terminar.")]
    [SerializeField] private GameObject _screenOnVisual;

    [Header("Eventos")]
    [SerializeField] private UnityEvent _onMovieStart;

    [Tooltip("Se dispara UNA vez al terminar la pelicula. Cablear MonsterChaser.StartChase().")]
    [SerializeField] private UnityEvent _onMovieEnd;

    private bool _played;

    public void Play()
    {
        if (_played)
        {
            return;
        }
        _played = true;

        if (_screenOnVisual != null) _screenOnVisual.SetActive(true);
        _onMovieStart?.Invoke();
        Debug.Log("[TVMovie] Pelicula arrancando.");

        if (!_useTimerInsteadOfVideo && _videoPlayer != null)
        {
            _videoPlayer.isLooping = false;
            _videoPlayer.loopPointReached += OnVideoEnd;
            _videoPlayer.Play();
        }
        else
        {
            StartCoroutine(TimerEnd());
        }
    }

    private IEnumerator TimerEnd()
    {
        yield return new WaitForSeconds(Mathf.Max(0.5f, _movieDuration));
        End();
    }

    private void OnVideoEnd(VideoPlayer vp)
    {
        End();
    }

    private void End()
    {
        if (_videoPlayer != null) _videoPlayer.loopPointReached -= OnVideoEnd;
        if (_screenOnVisual != null) _screenOnVisual.SetActive(false);
        Debug.Log("[TVMovie] Pelicula terminada -> aparece el monstruo.");
        _onMovieEnd?.Invoke();
    }
}
