using UnityEngine;

/// <summary>
/// Hace flotar y girar despacio un objeto (los fragmentos de la nota de
/// Parte 5: papeles que flotan en el aire, como un recuerdo distorsionado, y
/// que se distinguen de lejos entre tanto amarillo). Solo mueve el Transform
/// de este objeto: el Collider de la mirada conviene ponerlo en el padre, que
/// queda quieto.
/// </summary>
public class FloatingBob : MonoBehaviour
{
    [SerializeField] private float _bobHeight = 0.08f;
    [SerializeField] private float _bobSpeed = 1.1f;
    [SerializeField] private float _spinDegreesPerSecond = 25f;

    private Vector3 _startLocal;
    private float _phase;

    private void Awake()
    {
        _startLocal = transform.localPosition;
        _phase = Random.Range(0f, Mathf.PI * 2f);
    }

    private void Update()
    {
        transform.localPosition = _startLocal + Vector3.up * (Mathf.Sin(Time.time * _bobSpeed + _phase) * _bobHeight);
        transform.Rotate(0f, _spinDegreesPerSecond * Time.deltaTime, 0f, Space.World);
    }
}
