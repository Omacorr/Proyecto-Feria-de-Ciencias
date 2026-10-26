using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Cuenta veces y, al llegar a un numero, dispara un evento. Pensado para
/// "despues de N vueltas del loop, dar una pista" (Parte 4: el TeleportPoint
/// del loop llama Count() en su On Loop; a la 2da vuelta suena un golpe del
/// lado de la pintura y parpadea la lampara de ese tramo).
///
/// Repeat Every = 0: dispara una sola vez. Mayor a 0: vuelve a disparar cada
/// esa cantidad de cuentas extra (por ejemplo 2 = a la 2da, 4ta, 6ta...).
/// </summary>
public class CountedEvent : MonoBehaviour
{
    [Tooltip("A cuantas cuentas se dispara On Reached por primera vez.")]
    [SerializeField] private int _threshold = 2;
    [Tooltip("0 = una sola vez. Mayor a 0 = se repite cada esta cantidad de cuentas despues de la primera.")]
    [SerializeField] private int _repeatEvery = 2;
    [SerializeField] private UnityEvent _onReached;

    public int Current { get; private set; }

    public void Count()
    {
        Current++;
        // Threshold 0 o negativo en el Inspector = a la primera cuenta (antes
        // nunca disparaba la "primera vez" y el repetir quedaba corrido).
        int threshold = Mathf.Max(1, _threshold);
        bool first = Current == threshold;
        bool again = _repeatEvery > 0 && Current > threshold && (Current - threshold) % _repeatEvery == 0;
        if (first || again)
        {
            Debug.Log("[CountedEvent] " + name + ": " + Current + " -> On Reached");
            _onReached?.Invoke();
        }
    }

    public void ResetCount()
    {
        Current = 0;
    }
}
