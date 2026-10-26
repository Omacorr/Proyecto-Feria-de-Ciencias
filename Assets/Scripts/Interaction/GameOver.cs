using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// "Te atraparon" — reinicia la escena actual con un fundido y un cartel.
/// Pensado para cablear MonsterChaser._onCaught a RestartLevel().
/// </summary>
public class GameOver : MonoBehaviour
{
    [SerializeField] private string _bigText = "TE ATRAPARON";
    [SerializeField] private string _subText = "reintentando...";
    [SerializeField] private float _delayBeforeFade = 0.3f;

    private bool _going;

    /// <summary>Cablear desde MonsterChaser._onCaught.</summary>
    public void RestartLevel()
    {
        if (_going) return;
        _going = true;
        StartCoroutine(Routine());
    }

    private IEnumerator Routine()
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, _delayBeforeFade));
        string scene = SceneManager.GetActiveScene().name;
        SceneTransitionOverlay.Play(scene, _bigText, _subText, 0.6f, 1.6f, Color.black);
    }
}
