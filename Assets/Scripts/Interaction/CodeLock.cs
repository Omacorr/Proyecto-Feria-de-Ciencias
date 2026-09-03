using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Puzzle de combinacion (Etapa 1, "El Despertar"). Varios CodeDigit
/// muestran cada uno un simbolo; cada vez que alguno termina de girar,
/// este script arma la palabra completa leyendolos en orden y la compara
/// contra _targetCode. Si coincide, dispara _onUnlocked (UnityEvent -
/// cablealo en el Inspector a lo que necesites: abrir una puerta/alacena
/// reusando Door con Requires Key destildado, o directo a
/// KeyInventory.CollectKey()).
/// </summary>
public class CodeLock : MonoBehaviour
{
    [Tooltip("Los CodeDigit que forman la combinacion, en orden de izquierda a derecha.")]
    [SerializeField] private CodeDigit[] _digits;

    [Tooltip("La combinacion correcta, con los mismos simbolos que usan los CodeDigit (por ejemplo 'BAD'). Tiene que tener la misma cantidad de letras que elementos en Digits.")]
    [SerializeField] private string _targetCode = "AAA";

    [SerializeField] private UnityEvent _onUnlocked;

    public bool IsUnlocked { get; private set; }

    private void OnEnable()
    {
        // Reinicia el estado en cada partida nueva (vive bajo GameplayRoot,
        // igual que SequenceManager).
        IsUnlocked = false;
    }

    /// <summary>
    /// Llamado por cualquier CodeDigit cada vez que termina de girar a un
    /// simbolo nuevo.
    /// </summary>
    public void ReportDigitChanged()
    {
        if (IsUnlocked || _digits == null || _digits.Length == 0)
        {
            return;
        }

        if (BuildCurrentCode() == _targetCode)
        {
            IsUnlocked = true;
            Debug.Log("[CodeLock] Combinacion correcta.");
            _onUnlocked?.Invoke();
        }
    }

    private string BuildCurrentCode()
    {
        var chars = new char[_digits.Length];
        for (int i = 0; i < _digits.Length; i++)
        {
            chars[i] = _digits[i] != null ? _digits[i].CurrentSymbol : '\0';
        }
        return new string(chars);
    }
}
