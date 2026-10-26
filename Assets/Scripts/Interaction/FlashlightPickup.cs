using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Hace que la linterna del jugador (Light hija de Main Camera, con
/// FlickeringLight y FlashlightSway) arranque APAGADA y se prenda recien al
/// recolectarla. Cablear Collectable.OnCollected -> FlashlightPickup.PickUp().
///
/// FlickeringLight solo escribe Light.intensity (nunca toca Light.enabled), asi
/// que apagar la linterna con Light.enabled = false es suficiente; ademas se
/// llama FlickeringLight.TurnOff()/TurnOn() para que no siga calculando
/// parpadeos con la luz muerta.
///
/// Examinar: si otro script (eventos de ExamineTrigger) tiene que apagar la
/// linterna mientras se examina, NO debe tocar Light.enabled directo (la
/// prenderia antes de recogerla). Usar SetTemporarilyOff(true/false) (instancia,
/// para UnityEvent) o FlashlightPickup.SetFlashlightTemporarilyOff(bool)
/// (estatico, desde codigo): solo vuelve a prender si ya se recogio.
/// </summary>
public class FlashlightPickup : MonoBehaviour
{
    [Tooltip("La Light de la linterna (hija de Main Camera).")]
    [SerializeField] private Light _flashlight;

    [Tooltip("Opcional: FlickeringLight de la linterna. Si queda vacio se busca en la misma Light.")]
    [SerializeField] private FlickeringLight _flicker;

    [Tooltip("Si esta tildado, la linterna arranca apagada hasta recogerla. Destildado: arranca prendida (no hace nada al inicio).")]
    [SerializeField] private bool _startOff = true;

    [Header("Sonido (opcional)")]
    [Tooltip("AudioSource (2D) desde donde suena el clic de la linterna.")]
    [SerializeField] private AudioSource _clickSource;

    [Tooltip("Clic al prender la linterna (clic_linterna).")]
    [SerializeField] private AudioClip _clickClip;

    [Header("Objeto recogido (opcional)")]
    [Tooltip("Luz que marca la linterna sobre el armario; se apaga al recogerla.")]
    [SerializeField] private Light _pickupGlow;

    [Header("Mensaje (opcional)")]
    [Tooltip("Texto en VR que se muestra al recogerla.")]
    [SerializeField] private VRHintText _hint;

    [SerializeField] private string _pickupMessage = "Una linterna. Ahora sí veo algo.";

    [Tooltip("Se dispara una sola vez al recogerla.")]
    [SerializeField] private UnityEvent _onPickedUp;

    private static FlashlightPickup _instance;

    private bool _temporarilyOff;

    /// <summary>True si la linterna ya esta en poder del jugador.</summary>
    public bool HasFlashlight { get; private set; }

    private void Awake()
    {
        _instance = this;

        if (_flicker == null && _flashlight != null)
        {
            _flicker = _flashlight.GetComponent<FlickeringLight>();
        }

        // Si no arranca apagada, se considera ya recogida.
        HasFlashlight = !_startOff;
        ApplyState();
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    /// <summary>Recoge la linterna: la prende, suena el clic y muestra el mensaje. Una sola vez.</summary>
    public void PickUp()
    {
        if (HasFlashlight)
        {
            return;
        }

        HasFlashlight = true;
        ApplyState();

        if (_pickupGlow != null)
        {
            _pickupGlow.enabled = false;
        }

        if (_clickSource != null && _clickClip != null)
        {
            _clickSource.PlayOneShot(_clickClip);
        }

        if (_hint != null && !string.IsNullOrEmpty(_pickupMessage))
        {
            _hint.Show(_pickupMessage);
        }

        _onPickedUp?.Invoke();
    }

    /// <summary>
    /// Apaga/restaura la linterna de forma temporal (por ejemplo al examinar el
    /// candado). Restaurar solo la prende si ya fue recogida. Llamable desde
    /// UnityEvent (dropdown con bool).
    /// </summary>
    public void SetTemporarilyOff(bool off)
    {
        _temporarilyOff = off;
        ApplyState();
    }

    /// <summary>Version estatica de SetTemporarilyOff; no hace nada si no hay FlashlightPickup en la escena.</summary>
    public static void SetFlashlightTemporarilyOff(bool off)
    {
        if (_instance != null)
        {
            _instance.SetTemporarilyOff(off);
        }
    }

    private void ApplyState()
    {
        bool on = HasFlashlight && !_temporarilyOff;

        if (_flashlight != null)
        {
            _flashlight.enabled = on;
        }

        if (_flicker != null)
        {
            if (on)
            {
                _flicker.TurnOn();
            }
            else
            {
                _flicker.TurnOff();
            }
        }
    }
}
