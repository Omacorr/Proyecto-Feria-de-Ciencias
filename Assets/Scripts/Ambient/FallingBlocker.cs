using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Un mueble (el armario de horror_room en Parte 4) que se vuelca detras del
/// jugador y le cierra el paso: cruje, cae girando sobre el borde de su base
/// (acelerando, como algo pesado), golpea con un rebote chico y sacude la
/// camara. Al terminar apaga los GameObjects de los puntos que quedaron del
/// otro lado (apagar solo el componente TeleportPoint no los saca de la
/// mirada, gotcha #20) y dispara On Fallen. Una sola vez.
///
/// Armado: este componente va en la BISAGRA (un vacio en el borde de la base
/// sobre el que gira el mueble) y el modelo es hijo de la bisagra. Fall Axis
/// es el eje local de la bisagra; con el signo de Fall Angle se elige hacia
/// donde cae. Trigger() se llama desde un UnityEvent (por ejemplo el On Player
/// Arrived del tp siguiente).
/// </summary>
public class FallingBlocker : MonoBehaviour
{
    [Header("Caida")]
    [Tooltip("Eje LOCAL de la bisagra sobre el que gira el mueble.")]
    [SerializeField] private Vector3 _fallAxis = Vector3.right;
    [Tooltip("Grados que gira (el signo elige el lado). 90 = queda acostado.")]
    [SerializeField] private float _fallAngle = 90f;
    [Tooltip("Segundos desde el crujido hasta que empieza a caer.")]
    [SerializeField] private float _creakLead = 0.9f;
    [Tooltip("Segundos de la caida (acelera como un cuerpo pesado).")]
    [SerializeField] private float _fallDuration = 0.65f;
    [Tooltip("Grados del rebote al golpear el piso.")]
    [SerializeField] private float _bounceAngle = 4f;
    [Tooltip("Tambaleo previo (grados) mientras cruje, antes de caer.")]
    [SerializeField] private float _wobbleAngle = 3f;

    [Header("Sonido (3D, en el mueble)")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _creakClip;
    [SerializeField] private AudioClip[] _impactClips;
    [Range(0f, 1f)] [SerializeField] private float _impactVolume = 1f;

    [Header("Sacudida de camara")]
    [SerializeField] private Transform _cameraTransform;
    [SerializeField] private float _shakeAmount = 0.07f;
    [SerializeField] private float _shakeDuration = 0.55f;

    [Header("Despues de caer")]
    [Tooltip("Se APAGAN (SetActive false) al caer: los tp que quedaron del otro lado.")]
    [SerializeField] private GameObject[] _disableOnFall;
    [Tooltip("Se PRENDEN al caer (por ejemplo un collider que tapa la mirada).")]
    [SerializeField] private GameObject[] _enableOnFall;
    [SerializeField] private UnityEvent _onFallen = new UnityEvent();

    private bool _done;
    private Quaternion _startRot;

    public bool HasFallen => _done;

    private void Awake()
    {
        _startRot = transform.localRotation;
    }

    /// <summary>Vuelca el mueble (solo la primera vez).</summary>
    public void Trigger()
    {
        if (_done || !isActiveAndEnabled)
        {
            return;
        }
        _done = true;
        StartCoroutine(Sequence());
    }

    private IEnumerator Sequence()
    {
        Debug.Log("[FallingBlocker] " + name + ": se cae.");
        Vector3 axis = _fallAxis.sqrMagnitude > 0f ? _fallAxis.normalized : Vector3.right;

        // 1. Cruje y se tambalea.
        if (_audioSource != null && _creakClip != null)
        {
            _audioSource.PlayOneShot(_creakClip);
        }
        float t = 0f;
        while (t < _creakLead)
        {
            t += Time.deltaTime;
            float k = t / Mathf.Max(0.01f, _creakLead);
            float wobble = Mathf.Sin(t * 22f) * _wobbleAngle * k * Mathf.Sign(_fallAngle);
            transform.localRotation = _startRot * Quaternion.AngleAxis(wobble, axis);
            yield return null;
        }

        // 2. Cae acelerando (angulo ~ t^2).
        t = 0f;
        while (t < _fallDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(0.01f, _fallDuration));
            transform.localRotation = _startRot * Quaternion.AngleAxis(_fallAngle * k * k, axis);
            yield return null;
        }
        transform.localRotation = _startRot * Quaternion.AngleAxis(_fallAngle, axis);

        // 3. Golpe: sonido, se cierra el paso, sacudida y rebote.
        if (_audioSource != null && _impactClips != null)
        {
            foreach (AudioClip c in _impactClips)
            {
                if (c != null)
                {
                    _audioSource.PlayOneShot(c, _impactVolume);
                }
            }
        }
        SetAll(_disableOnFall, false);
        SetAll(_enableOnFall, true);
        StartCoroutine(Shake());

        float bounceTime = 0.35f;
        t = 0f;
        float sign = Mathf.Sign(_fallAngle);
        while (t < bounceTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / bounceTime);
            float back = Mathf.Sin(k * Mathf.PI) * _bounceAngle * (1f - k * 0.5f);
            transform.localRotation = _startRot * Quaternion.AngleAxis(_fallAngle - back * sign, axis);
            yield return null;
        }
        transform.localRotation = _startRot * Quaternion.AngleAxis(_fallAngle, axis);

        _onFallen.Invoke();
    }

    private IEnumerator Shake()
    {
        Transform cam = _cameraTransform != null ? _cameraTransform : (Camera.main != null ? Camera.main.transform : null);
        if (cam == null || _shakeAmount <= 0f)
        {
            yield break;
        }
        var pivot = new CameraPivot(cam, "__FallingBlockerShake");
        Vector3 basePos = pivot.Pivot.localPosition;
        float t = 0f;
        while (t < _shakeDuration && pivot.Pivot != null)
        {
            t += Time.deltaTime;
            float a = _shakeAmount * (1f - t / _shakeDuration);
            pivot.Pivot.localPosition = basePos + new Vector3(
                (Mathf.PerlinNoise(t * 30f, 0f) - 0.5f) * 2f * a,
                (Mathf.PerlinNoise(0f, t * 30f) - 0.5f) * 2f * a,
                0f);
            yield return null;
        }
        if (pivot.Pivot != null)
        {
            pivot.Pivot.localPosition = basePos;
        }
        pivot.Release();
    }

    private static void SetAll(GameObject[] objs, bool active)
    {
        if (objs == null)
        {
            return;
        }
        foreach (GameObject go in objs)
        {
            if (go != null)
            {
                go.SetActive(active);
            }
        }
    }
}
