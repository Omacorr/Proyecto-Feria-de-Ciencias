using System.Collections;
using UnityEngine;

/// <summary>
/// Cumpleaños (Parte 2): el momento "te das vuelta y el monstruo ya viene".
///
/// Se cablea desde SecurityFootage._onMovieEnd (en lugar de ir directo a
/// MonsterChaser.StartChase). Hace la previa de la persecucion:
///  1. suena un golpe/rugido desde un AudioSource que conviene poner DETRAS
///     del jugador (en la habitacion de atras), para que gire la cabeza solo;
///  2. un parpadeo a negro (VRFadeController) para tapar el "corte";
///  3. mientras esta todo negro, reubica al monstruo en su punto de aparicion
///     y llama a MonsterChaser.StartChase();
///  4. vuelve la imagen y el monstruo ya esta ahi, caminando.
///
/// NO gira la camara sola: en Cardboard el TrackedPoseDriver pisa la rotacion
/// con la del casco cada frame, asi que forzar el giro pelea con el hardware y marea.
/// </summary>
public class ChaseReveal : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("El monstruo que persigue (Monstruo_Menu).")]
    [SerializeField] private MonsterChaser _chaser;

    [Tooltip("Para el parpadeo a negro. Si se deja vacio, se busca uno en la escena al arrancar.")]
    [SerializeField] private VRFadeController _fade;

    [Header("Revelado")]
    [Tooltip("Si no hay VRFadeController, cuanto esperar antes de mostrar al monstruo y arrancar la persecucion.")]
    [SerializeField] private float _delayBeforeChase = 0.5f;

    [Tooltip("El monstruo se teletransporta EXACTO a la posicion/rotacion de este Transform (parado SOBRE EL PISO en el umbral de la habitacion de atras, apuntando hacia donde viene el jugador; solo se usa el giro horizontal). Vacio = aparece donde esta.")]
    [SerializeField] private Transform _monsterRevealPoint;

    [Header("Golpe de sonido (opcional)")]
    [Tooltip("AudioSource desde donde suena el golpe/rugido. Ponelo DETRAS del jugador para que gire la cabeza hacia el.")]
    [SerializeField] private AudioSource _stingerSource;
    [SerializeField] private AudioClip _stingerClip;

    private bool _done;

    /// <summary>Cablear desde SecurityFootage._onMovieEnd.</summary>
    public void Play()
    {
        if (_done)
        {
            return;
        }
        _done = true;

        if (_fade == null)
        {
            _fade = FindFirstObjectByType<VRFadeController>();
        }
        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        if (_stingerSource != null && _stingerClip != null)
        {
            _stingerSource.PlayOneShot(_stingerClip);
        }

        if (_fade == null)
        {
            if (_delayBeforeChase > 0f)
            {
                yield return new WaitForSeconds(_delayBeforeChase);
            }
            Reveal();
            yield break;
        }

        // Si hay un fundido en curso (p.ej. otra transicion), esperar a que termine
        // (con tope de 4 s para no trabarse).
        for (float waited = 0f; _fade.IsFading && waited < 4f; waited += Time.unscaledDeltaTime)
        {
            yield return null;
        }
        // FadeOutAndIn ejecuta Reveal() cuando la pantalla esta 100% negra.
        yield return _fade.StartCoroutine(_fade.FadeOutAndIn(Reveal));
    }

    private void Reveal()
    {
        if (_chaser == null)
        {
            Debug.LogWarning("[ChaseReveal] Falta asignar el MonsterChaser.");
            return;
        }

        if (_monsterRevealPoint != null)
        {
            Vector3 f = _monsterRevealPoint.forward;
            f.y = 0f;
            Quaternion rot = f.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(f, Vector3.up) : _chaser.transform.rotation;
            _chaser.transform.SetPositionAndRotation(_monsterRevealPoint.position, rot);
        }
        _chaser.StartChase();
    }
}
