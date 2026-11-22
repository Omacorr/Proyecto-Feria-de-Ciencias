using System.Collections;
using UnityEngine;

/// <summary>
/// "Despertas tirado en el piso" (guion, etapa 5): al cargar la escena la
/// camara arranca a la altura del piso y se levanta despacio hasta la altura
/// de ojos que tiene guardada la escena. Mientras tanto la mirada queda
/// bloqueada, para no elegir un punto de teletransporte acostado (la caminata
/// de TeleportManager conserva la altura de ojos con la que arranca: un tp
/// elegido a mitad de levantarse dejaria al jugador bajo para todo el nivel).
///
/// La mirada se bloquea con GazeController.AddBlocker (un bloqueo con dueño),
/// NO apagando el componente: FaintOverlay tambien apaga/prende 'enabled' al
/// llegar con un desmayo, y asi ninguno de los dos puede devolverle la mirada
/// al jugador mientras el otro todavia la necesita bloqueada, sin importar en
/// que orden corran.
///
/// Solo mueve la ALTURA de la camara (la rotacion es del casco). Va en
/// cualquier objeto de la escena; la camara se toma de Camera.main si no se
/// asigna. Si este objeto se apaga o se destruye a mitad, la camara queda
/// parada y la mirada se libera (nunca deja al jugador trabado).
/// </summary>
public class WakeUpFromFloor : MonoBehaviour
{
    [SerializeField] private Transform _camera;
    [Tooltip("Cuantos metros mas abajo arranca la camara (altura de alguien tirado en el piso).")]
    [SerializeField] private float _floorOffset = 1.25f;
    [Tooltip("Segundos quieto en el piso antes de empezar a levantarse.")]
    [SerializeField] private float _delay = 2.5f;
    [Tooltip("Segundos que tarda en quedar parado.")]
    [SerializeField] private float _riseSeconds = 3f;

    private bool _running;
    private float _standY;

    private IEnumerator Start()
    {
        if (_camera == null && Camera.main != null)
        {
            _camera = Camera.main.transform;
        }
        if (_camera == null)
        {
            yield break;
        }

        _running = true;
        GazeController.AddBlocker(this);

        _standY = _camera.position.y;
        float lyingY = _standY - _floorOffset;
        SetY(lyingY);
        yield return new WaitForSeconds(_delay);

        float t = 0f;
        float rise = Mathf.Max(0.01f, _riseSeconds);
        while (t < rise)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / rise);
            // Primero lento (cuesta levantarse), despues mas rapido, y se asienta.
            SetY(Mathf.Lerp(lyingY, _standY, k * k * (3f - 2f * k)));
            yield return null;
        }

        Finish();
    }

    /// <summary>Deja la camara parada y libera la mirada (una sola vez).</summary>
    private void Finish()
    {
        if (!_running)
        {
            return;
        }
        _running = false;
        if (_camera != null)
        {
            SetY(_standY);
        }
        GazeController.RemoveBlocker(this);
    }

    // Unity corta la corrutina si se apaga el objeto: sin esto la camara
    // quedaba acostada y la mirada bloqueada para siempre.
    // (Si solo se destilda el componente la corrutina seguiria: se corta a mano.)
    private void OnDisable()
    {
        StopAllCoroutines();
        Finish();
    }

    private void OnDestroy()
    {
        GazeController.RemoveBlocker(this);
    }

    private void SetY(float y)
    {
        Vector3 p = _camera.position;
        _camera.position = new Vector3(p.x, y, p.z);
    }
}
