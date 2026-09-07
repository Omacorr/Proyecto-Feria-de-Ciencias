using UnityEngine;

/// <summary>
/// LOG TEMPORAL. Se pone en Main Camera nada mas para diagnosticar la vista
/// rota que aparecio despues de separar Main Camera de Player (candado
/// "sliver" flotando en el aire). Imprime cada 1 segundo la posicion y
/// rotacion REALES de Main Camera en tiempo de ejecucion en el celular, asi
/// las podemos comparar con lo que deberia ser (cerca de Player, dentro del
/// cuarto) contra lo que el sensor del casco realmente esta aplicando.
/// Sacar este script (y el componente del Inspector) despues de resolver
/// el bug.
/// </summary>
public class CameraDebugLogger : MonoBehaviour
{
    private float _timer;

    private void Update()
    {
        _timer += Time.deltaTime;
        if (_timer < 1f)
        {
            return;
        }

        _timer = 0f;
        Debug.Log($"[CameraDebugLogger] pos={transform.position} rot={transform.rotation.eulerAngles} parent={(transform.parent != null ? transform.parent.name : "ninguno")} XRenabled={UnityEngine.XR.XRSettings.enabled} XRdevice={UnityEngine.XR.XRSettings.loadedDeviceName} stereo={UnityEngine.XR.XRSettings.isDeviceActive}");
    }
}
