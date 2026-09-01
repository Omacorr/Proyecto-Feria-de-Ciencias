using UnityEngine;

/// <summary>
/// Loop de juego minimo: Menu -> Jugando -> Victoria/Derrota -> vuelve a Menu.
/// No usa escenas separadas: activa/desactiva grupos de GameObjects segun el
/// estado. Los "botones" (Empezar, condicion de victoria/derrota, Volver al
/// menu) son objetos InteractiveObject comunes -- no hacen falta scripts
/// nuevos para ellos, solo conectar su evento OnSelected (en el Inspector)
/// a StartGame(), TriggerVictory(), TriggerDefeat() o ReturnToMenu().
/// </summary>
public class GameManager : MonoBehaviour
{
    public enum GameState
    {
        MainMenu,
        Playing,
        Victory,
        Defeat
    }

    [Header("Paneles/objetos por estado")]
    [SerializeField] private GameObject _mainMenuRoot;
    [SerializeField] private GameObject _victoryRoot;
    [SerializeField] private GameObject _defeatRoot;

    [Tooltip("Raiz que agrupa todo lo jugable (cuarto, objetos interactivos, teleport points). Se activa solo durante Playing.")]
    [SerializeField] private GameObject _gameplayRoot;

    public GameState CurrentState { get; private set; }

    private void Start()
    {
        SetState(GameState.MainMenu);
    }

    public void StartGame()
    {
        SetState(GameState.Playing);
    }

    public void TriggerVictory()
    {
        if (CurrentState != GameState.Playing)
        {
            return;
        }

        SetState(GameState.Victory);
    }

    public void TriggerDefeat()
    {
        if (CurrentState != GameState.Playing)
        {
            return;
        }

        SetState(GameState.Defeat);
    }

    public void ReturnToMenu()
    {
        SetState(GameState.MainMenu);
    }

    private void SetState(GameState newState)
    {
        CurrentState = newState;
        Debug.Log($"[GameManager] Estado: {newState}");

        if (_mainMenuRoot != null)
        {
            _mainMenuRoot.SetActive(newState == GameState.MainMenu);
        }

        if (_victoryRoot != null)
        {
            _victoryRoot.SetActive(newState == GameState.Victory);
        }

        if (_defeatRoot != null)
        {
            _defeatRoot.SetActive(newState == GameState.Defeat);
        }

        if (_gameplayRoot != null)
        {
            _gameplayRoot.SetActive(newState == GameState.Playing);
        }
    }
}
