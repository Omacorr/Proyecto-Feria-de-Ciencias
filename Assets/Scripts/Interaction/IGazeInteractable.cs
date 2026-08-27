/// <summary>
/// Etapa 3 del MVP: contrato que debe implementar cualquier objeto que quiera
/// reaccionar a la mirada del jugador. El GazeController solo detecta y despacha
/// estos eventos - cada objeto decide que hacer con ellos (esa logica se separa
/// en la etapa 4, con InteractiveObject y Collectable).
/// </summary>
public interface IGazeInteractable
{
    /// <summary>Se llama una vez, el frame en que la mirada entra en el objeto.</summary>
    void OnGazeEnter();

    /// <summary>
    /// Se llama en cada frame mientras la mirada se mantiene sobre el objeto.
    /// </summary>
    /// <param name="progress">
    /// Progreso hacia la seleccion, de 0 (recien entro) a 1 (listo para seleccionar).
    /// Pensado para que la etapa 5 (reticulo) lo use como feedback visual.
    /// </param>
    void OnGazeStay(float progress);

    /// <summary>Se llama una vez, el frame en que la mirada sale del objeto.</summary>
    void OnGazeExit();

    /// <summary>
    /// Se llama cuando se mantuvo la mirada el tiempo suficiente como para
    /// considerarse una seleccion (equivalente a un "click" sin controles).
    /// </summary>
    void OnGazeSelect();
}
