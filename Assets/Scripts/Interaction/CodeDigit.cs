using System.Collections;
using UnityEngine;

/// <summary>
/// Un solo cilindro de letras de un CodeLock (por ejemplo, cada rueda del
/// candado de A a F). Al mirarlo y mantener la mirada, gira al siguiente
/// simbolo (en loop) y le avisa al CodeLock para que revise si la
/// combinacion completa ya es correcta. Este script no sabe cual es la
/// combinacion correcta - esa logica vive entera en CodeLock. Aca solo se
/// muestra un simbolo a la vez y se gira.
/// </summary>
public class CodeDigit : MonoBehaviour, IGazeInteractable
{
    [Tooltip("El CodeLock dueno de este cilindro. Le avisa cada vez que cambia de simbolo.")]
    [SerializeField] private CodeLock _codeLock;

    [Header("Simbolos")]
    [Tooltip("Los simbolos posibles, en el orden en que aparecen al girar (por ejemplo 'ABCDEF'). El primero es el que arranca mostrando.")]
    [SerializeField] private string _symbols = "ABCDEF";

    [Header("Rotacion")]
    [Tooltip("Eje LOCAL sobre el que gira el cilindro. Probalo en el editor: si gira 'acostado' en vez de sobre su propio eje, es que este vector esta mal - cambialo (por ejemplo a Vector3.up o Vector3.forward) hasta que gire como corresponde.")]
    [SerializeField] private Vector3 _rotationAxis = Vector3.right;

    [Tooltip("Cuantos grados gira por cada simbolo. Normalmente 360 dividido la cantidad de simbolos visibles en el modelo (6 simbolos = 60).")]
    [SerializeField] private float _degreesPerStep = 60f;

    [Tooltip("Cuantos segundos tarda en girar de un simbolo al siguiente. Dejalo en 0 para que sea instantaneo.")]
    [SerializeField] private float _rotationDuration = 0.15f;

    [Header("Feedback de mirada (opcional)")]
    [SerializeField] private Material _inactiveMaterial;
    [SerializeField] private Material _gazedAtMaterial;

    /// <summary>Simbolo que esta mostrando este cilindro ahora mismo.</summary>
    public char CurrentSymbol => !string.IsNullOrEmpty(_symbols) ? _symbols[_currentIndex] : '\0';

    private Renderer _renderer;
    private int _currentIndex;
    private bool _isRotating;

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        SetGazed(false);
    }

    private void OnEnable()
    {
        // Vuelve al primer simbolo cada vez que arranca una partida nueva
        // (este objeto vive bajo GameplayRoot, igual que SequenceStep).
        _currentIndex = 0;
        _isRotating = false;
    }

    public void OnGazeEnter()
    {
        SetGazed(true);
    }

    public void OnGazeStay(float progress)
    {
        // El feedback de progreso ya lo muestra GazeReticle.
    }

    public void OnGazeExit()
    {
        SetGazed(false);
    }

    public void OnGazeSelect()
    {
        // LOG TEMPORAL: confirma que este cilindro puntual es el que
        // realmente recibio la seleccion (y no el candado entero). Sacar
        // despues de resolver el bug de la hitbox.
        Debug.Log($"[CodeDigit] OnGazeSelect en {gameObject.name} (isRotating={_isRotating})");

        if (_isRotating || string.IsNullOrEmpty(_symbols))
        {
            return;
        }

        _currentIndex = (_currentIndex + 1) % _symbols.Length;
        StartCoroutine(RotateStep());

        _codeLock?.ReportDigitChanged();
    }

    private IEnumerator RotateStep()
    {
        _isRotating = true;

        Quaternion fromRot = transform.localRotation;
        Quaternion toRot = fromRot * Quaternion.AngleAxis(_degreesPerStep, _rotationAxis);

        float duration = Mathf.Max(0f, _rotationDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transform.localRotation = Quaternion.Slerp(fromRot, toRot, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        transform.localRotation = toRot;
        _isRotating = false;
    }

    private void SetGazed(bool gazedAt)
    {
        if (_renderer != null && _inactiveMaterial != null && _gazedAtMaterial != null)
        {
            _renderer.material = gazedAt ? _gazedAtMaterial : _inactiveMaterial;
        }
    }
}
