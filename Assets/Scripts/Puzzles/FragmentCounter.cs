using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Contador de fragmentos de la nota (Etapa 5, "Backrooms"). Cada fragmento es
/// un Collectable comun (Collider + Layer Interactive + componente Collectable)
/// cuyo evento On Collected llama a AddFragment() aca. Al juntar la cantidad
/// pedida (3 por defecto) dispara On All Collected UNA vez.
///
/// Lo usa FragmentDropOff (el punto de entrega en el centro del mapa) para
/// saber si ya se puede entregar. No es un singleton: va UNO por escena, en un
/// objeto vacio, y todos los fragmentos y el punto de entrega apuntan a ESE
/// mismo objeto (mismo criterio que KeyInventory).
///
/// Feedback opcional: un texto TMP (World Space, hijo de Main Camera, como el
/// ReticleCanvas) que muestra unos segundos el texto del fragmento recien
/// juntado o "Fragmentos 1/3", y un sonido por fragmento y otro al completar.
///
/// OJO al cablear (gotcha #9 de CLAUDE.md): cada Collectable tiene que llamar
/// AddFragment() UNA sola vez (una fila en su On Collected, no dos), si no se
/// cuenta doble.
/// </summary>
public class FragmentCounter : MonoBehaviour
{
    [Tooltip("Cuantos fragmentos hacen falta.")]
    [SerializeField] private int _requiredCount = 3;

    [Header("Texto (opcional)")]
    [Tooltip("Texto TMP donde se muestra el aviso (TextMeshPro 3D o TextMeshProUGUI dentro de un Canvas World Space hijo de Main Camera). Arranca oculto.")]
    [SerializeField] private TMP_Text _label;

    [Tooltip("Formato del aviso. {0} = juntados, {1} = total. Se usa si no hay texto propio para ese fragmento.")]
    [SerializeField] private string _labelFormat = "Fragmentos: {0}/{1}";

    [Tooltip("Opcional: texto que revela cada fragmento, EN ORDEN DE RECOLECCION (el primero que se junte muestra el elemento 0, etc.), para que la nota se vaya armando. Si esta vacio se usa Label Format.")]
    [TextArea(2, 4)]
    [SerializeField] private string[] _fragmentTexts;

    [Tooltip("Texto al juntar todos. Vacio = se usa el del ultimo fragmento / Label Format.")]
    [TextArea(2, 4)]
    [SerializeField] private string _allCollectedText = "";

    [Tooltip("Segundos que queda visible el texto.")]
    [SerializeField] private float _labelDuration = 3.5f;

    [Header("Audio (opcional)")]
    [Tooltip("AudioSource (2D) desde donde suenan los clips. Tiene que estar en un objeto que NO se desactive (no en el fragmento).")]
    [SerializeField] private AudioSource _audioSource;

    [Tooltip("Sonido al juntar cada fragmento (papel).")]
    [SerializeField] private AudioClip _fragmentSound;

    [Tooltip("Sonido al juntar el ultimo (reemplaza al de arriba en ese momento).")]
    [SerializeField] private AudioClip _allCollectedSound;

    [Header("Eventos")]
    [Tooltip("Se dispara con cada fragmento. El int es cuantos van (1, 2, 3...).")]
    [SerializeField] private UnityEvent<int> _onFragmentCollected;

    [Tooltip("Se dispara UNA vez, al juntar el ultimo. Ej: prender la luz del punto de entrega del centro.")]
    [SerializeField] private UnityEvent _onAllCollected;

    public int Count { get; private set; }
    public int RequiredCount => _requiredCount;
    public bool HasAll => Count >= _requiredCount;
    public int Missing => Mathf.Max(0, _requiredCount - Count);

    private Coroutine _labelRoutine;

    private void Awake()
    {
        if (_label != null)
        {
            _label.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Llamar desde Collectable.On Collected de cada fragmento.
    /// </summary>
    public void AddFragment()
    {
        if (HasAll)
        {
            return;
        }

        Count++;
        bool completed = HasAll;
        Debug.Log($"[FragmentCounter] Fragmento {Count}/{_requiredCount}.");

        Play(completed && _allCollectedSound != null ? _allCollectedSound : _fragmentSound);
        ShowLabel(BuildText(completed));

        _onFragmentCollected?.Invoke(Count);
        if (completed)
        {
            _onAllCollected?.Invoke();
        }
    }

    private string BuildText(bool completed)
    {
        if (completed && !string.IsNullOrEmpty(_allCollectedText))
        {
            return _allCollectedText;
        }

        int index = Count - 1;
        if (_fragmentTexts != null && index >= 0 && index < _fragmentTexts.Length && !string.IsNullOrEmpty(_fragmentTexts[index]))
        {
            return _fragmentTexts[index];
        }

        if (string.IsNullOrEmpty(_labelFormat))
        {
            return "";
        }

        try
        {
            return string.Format(_labelFormat, Count, _requiredCount);
        }
        catch (System.FormatException)
        {
            // Llaves mal escritas en el Inspector: mostrar el texto tal cual
            // en vez de cortar la recoleccion con una excepcion.
            return _labelFormat;
        }
    }

    private void ShowLabel(string text)
    {
        if (_label == null || string.IsNullOrEmpty(text))
        {
            return;
        }

        if (_labelRoutine != null)
        {
            StopCoroutine(_labelRoutine);
        }
        _labelRoutine = StartCoroutine(LabelRoutine(text));
    }

    private IEnumerator LabelRoutine(string text)
    {
        _label.text = text;
        _label.gameObject.SetActive(true);
        yield return new WaitForSeconds(_labelDuration);
        _label.gameObject.SetActive(false);
        _labelRoutine = null;
    }

    private void Play(AudioClip clip)
    {
        if (clip != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(clip);
        }
    }
}
