using System.Collections;
using UnityEngine;

/// <summary>
/// Cartel temporal: prende un GameObject unos segundos y lo vuelve a apagar.
/// Si ya estaba visible, reinicia el tiempo.
/// </summary>
public static class TimedSign
{
    /// <summary>Apaga el cartel (null-safe). Se usa en Awake: los carteles arrancan ocultos.</summary>
    public static void Hide(GameObject sign)
    {
        if (sign != null)
        {
            sign.SetActive(false);
        }
    }

    /// <summary>Muestra el cartel 'seconds' segundos. 'routine' guarda la corrutina para poder reiniciarla.</summary>
    public static void Show(MonoBehaviour host, GameObject sign, float seconds, ref Coroutine routine)
    {
        if (sign == null)
        {
            return;
        }
        if (routine != null)
        {
            host.StopCoroutine(routine);
        }
        routine = host.StartCoroutine(Run(sign, seconds));
    }

    private static IEnumerator Run(GameObject sign, float seconds)
    {
        sign.SetActive(true);
        yield return new WaitForSeconds(seconds);
        sign.SetActive(false);
    }
}
