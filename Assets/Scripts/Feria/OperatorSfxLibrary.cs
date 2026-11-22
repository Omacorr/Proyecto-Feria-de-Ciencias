using UnityEngine;

/// <summary>
/// Biblioteca de sonidos y monstruo que usa el operador de la feria
/// (OperatorScares). Vive en Assets/Resources/OperatorSfxLibrary.asset y lo
/// arma el menu Tools > Feria de Ciencias > Operador: armar biblioteca de sonidos
/// (Assets/Editor/OperatorSfxLibraryBuilder.cs). Si se agrega un clip nuevo,
/// sumar su nombre a OperatorScares.ClipNames y volver a correr el menu.
/// </summary>
[CreateAssetMenu(fileName = "OperatorSfxLibrary", menuName = "Feria de Ciencias/Biblioteca de sonidos del operador")]
public class OperatorSfxLibrary : ScriptableObject
{
    [Tooltip("Clips que el operador puede disparar por nombre (ver OperatorScares.ClipNames).")]
    public AudioClip[] clips;

    [Tooltip("Prefab del monstruo (Monstruo_Rig) para los sustos en escenas sin Screamer.")]
    public GameObject monsterPrefab;

    [Tooltip("Grito del screamer.")]
    public AudioClip screamClip;

    /// <summary>Clip por nombre de archivo (sin extension), sin distinguir mayusculas. Null si no esta.</summary>
    public AudioClip Find(string name)
    {
        if (string.IsNullOrEmpty(name) || clips == null)
        {
            return null;
        }
        foreach (AudioClip c in clips)
        {
            if (c != null && string.Equals(c.name, name, System.StringComparison.OrdinalIgnoreCase))
            {
                return c;
            }
        }
        return null;
    }
}
