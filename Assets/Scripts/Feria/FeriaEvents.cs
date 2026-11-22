using System;

/// <summary>
/// Contrato común entre los sistemas de la feria (pistas, portales, cronómetro y
/// comandos del operador). Es una clase estática de eventos para que esos sistemas
/// no se conozcan entre sí: cada uno publica o escucha acá.
/// </summary>
public static class FeriaEvents
{
    /// <summary>El operador (o cualquier script) pide una pista directa ya.</summary>
    public static event Action HintRequested;

    /// <summary>El sistema de pistas dio una pista: escena y escalón (1 ambiente, 2 voz, 3 directa).</summary>
    public static event Action<string, int> HintGiven;

    /// <summary>Cambió la opción "portales visibles / sutiles" (true = visibles).</summary>
    public static event Action<bool> PortalVisibilityChanged;

    public static void RequestHint() => HintRequested?.Invoke();

    public static void ReportHint(string sceneName, int step) => HintGiven?.Invoke(sceneName, step);

    public static void NotifyPortalVisibility(bool visible) => PortalVisibilityChanged?.Invoke(visible);
}
