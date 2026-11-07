using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Servidor de comandos del operador de la feria. Se crea solo al arrancar
/// (RuntimeInitializeOnLoadMethod) y sobrevive los cambios de escena.
///
/// Escucha en 127.0.0.1:<see cref="Port"/> del celular; la PC llega con
///   adb forward tcp:7777 tcp:7777
/// y se conecta a localhost:7777. Protocolo: una linea UTF-8 por comando, una
/// linea de respuesta por comando.
///
///   ping          -> pong
///   hint          -> ok                (FeriaEvents.RequestHint)
///   skip          -> ok | err <motivo> (dispara el desmayo de la escena)
///   menu          -> ok | err <motivo> (cancela la partida y vuelve al Menu)
///   name <texto>  -> ok | err <motivo>
///   status        -> status <running 0/1> <segundos F2> <escena>
///
/// Un hilo de fondo acepta UN cliente a la vez y encola las lineas; Update las
/// procesa en el hilo principal (la API de Unity no se puede tocar desde otro
/// hilo) y responde por el socket. Cada comando se loguea como
/// "[CRONO] cmd <comando> ok|err <motivo>". En el Editor tambien anda (probar
/// con cualquier cliente TCP local).
/// </summary>
public class OperatorCommandServer : MonoBehaviour
{
    public const int Port = 7777;

    private static OperatorCommandServer s_instance;

    private readonly ConcurrentQueue<string> _commands = new ConcurrentQueue<string>();
    private readonly object _writeLock = new object();

    private TcpListener _listener;
    private Thread _thread;
    private volatile bool _running;
    private TcpClient _client;     // protegido por _writeLock
    private StreamWriter _writer;  // protegido por _writeLock

    // ---------------- Creacion ----------------

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        s_instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (s_instance != null)
        {
            return;
        }
        GameObject go = new GameObject("OperatorCommandServer (feria)");
        DontDestroyOnLoad(go);
        s_instance = go.AddComponent<OperatorCommandServer>();
    }

    private void Awake()
    {
        if (s_instance != null && s_instance != this)
        {
            Destroy(gameObject);
            return;
        }
        s_instance = this;
        StartServer();
    }

    private void StartServer()
    {
        try
        {
            _listener = new TcpListener(IPAddress.Loopback, Port);
            _listener.Start();
        }
        catch (Exception e)
        {
            // Puerto ocupado u otro problema de red: el juego sigue igual, sin comandos.
            Debug.LogWarning("[OperatorCommandServer] No pude abrir el puerto " + Port + ": " + e.Message);
            _listener = null;
            return;
        }
        _running = true;
        _thread = new Thread(AcceptLoop) { IsBackground = true, Name = "OperatorCommandServer" };
        _thread.Start();
        Debug.Log("[OperatorCommandServer] Escuchando en 127.0.0.1:" + Port);
    }

    // ---------------- Hilo de red ----------------

    private void AcceptLoop()
    {
        while (_running)
        {
            TcpClient client = null;
            try
            {
                client = _listener.AcceptTcpClient();
            }
            catch (Exception)
            {
                // listener.Stop() desbloquea Accept con una excepcion: si estamos
                // cerrando, salir; si no, reintentar despues de un respiro.
                if (!_running)
                {
                    return;
                }
                Thread.Sleep(200);
                continue;
            }

            try
            {
                client.NoDelay = true;
                NetworkStream stream = client.GetStream();
                StreamReader reader = new StreamReader(stream, new UTF8Encoding(false));
                StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false)) { NewLine = "\n", AutoFlush = true };
                lock (_writeLock)
                {
                    _client = client;
                    _writer = writer;
                }

                string line;
                while (_running && (line = reader.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (line.Length > 0)
                    {
                        _commands.Enqueue(line);
                    }
                }
            }
            catch (Exception)
            {
                // Desconexion (cable, adb que se corta, cliente cerrado): volver a Accept.
            }
            finally
            {
                lock (_writeLock)
                {
                    if (_client == client)
                    {
                        _client = null;
                        _writer = null;
                    }
                }
                try { client.Close(); } catch (Exception) { }
            }
        }
    }

    private void Reply(string text)
    {
        lock (_writeLock)
        {
            if (_writer == null)
            {
                return;
            }
            try
            {
                _writer.WriteLine(text);
            }
            catch (Exception)
            {
                // El cliente se fue: la respuesta se pierde, el hilo vuelve a Accept.
            }
        }
    }

    private void StopServer()
    {
        _running = false;
        try { _listener?.Stop(); } catch (Exception) { }
        _listener = null;
        lock (_writeLock)
        {
            try { _client?.Close(); } catch (Exception) { }
            _client = null;
            _writer = null;
        }
        // No se hace Join: el hilo es de fondo y sale solo al cerrarse los sockets.
        _thread = null;
    }

    private void OnApplicationQuit()
    {
        StopServer();
    }

    private void OnDestroy()
    {
        StopServer();
        if (s_instance == this)
        {
            s_instance = null;
        }
    }

    // Al pausar la app (Android) no se toca nada: el hilo queda bloqueado
    // esperando y los comandos que lleguen se procesan al volver.
    private void OnApplicationPause(bool paused)
    {
    }

    // ---------------- Comandos (hilo principal) ----------------

    private void Update()
    {
        while (_commands.TryDequeue(out string line))
        {
            string reply;
            try
            {
                reply = Process(line);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                string cmd = FirstToken(line, out _);
                reply = "err exception";
                GameTimer.Log("cmd " + cmd + " err exception");
            }
            Reply(reply);
        }
    }

    private static string FirstToken(string line, out string rest)
    {
        int sp = line.IndexOf(' ');
        if (sp < 0)
        {
            rest = string.Empty;
            return line.ToLowerInvariant();
        }
        rest = line.Substring(sp + 1).Trim();
        return line.Substring(0, sp).ToLowerInvariant();
    }

    private string Process(string line)
    {
        string cmd = FirstToken(line, out string rest);
        switch (cmd)
        {
            case "ping":
                return Ok(cmd, "pong");

            case "hint":
                FeriaEvents.RequestHint();
                return Ok(cmd);

            case "skip":
                return Skip();

            case "menu":
                return GoToMenu();

            case "name":
                if (rest.Length == 0)
                {
                    return Err(cmd, "empty-name");
                }
                GameTimer.SetPlayerName(rest);
                return Ok(cmd);

            case "status":
                string status = "status " + (GameTimer.IsRunning ? "1" : "0") + " " +
                                GameTimer.Fmt(GameTimer.ElapsedSeconds) + " " +
                                SceneManager.GetActiveScene().name;
                return Ok(cmd, status);

            default:
                return Err(cmd.Length > 0 ? cmd : "?", "unknown");
        }
    }

    private static string Ok(string cmd, string reply = "ok")
    {
        GameTimer.Log("cmd " + cmd + " ok");
        return reply;
    }

    private static string Err(string cmd, string reason)
    {
        GameTimer.Log("cmd " + cmd + " err " + reason);
        return "err " + reason;
    }

    // skip: dispara el desmayo que sigue la historia. Si hay varios, se prefiere
    // el que NO va al Menu y, entre esos, el que va a la escena siguiente en
    // Build Settings. En Parte Final, si solo esta el que va al Menu, se usa ese.
    private string Skip()
    {
        const string cmd = "skip";
        if (FaintOverlay.IsRunning)
        {
            return Err(cmd, "busy");
        }

        FaintTransition[] all = FindObjectsByType<FaintTransition>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        string activeScene = SceneManager.GetActiveScene().name;
        string nextByIndex = NextSceneByBuildIndex();

        FaintTransition best = null;
        FaintTransition toMenu = null;
        foreach (FaintTransition ft in all)
        {
            if (ft == null || !ft.isActiveAndEnabled || ft.HasTriggered)
            {
                continue;
            }
            if (ft.NextSceneName == GameTimer.MenuSceneName)
            {
                if (toMenu == null) toMenu = ft;
                continue;
            }
            if (best == null || (ft.NextSceneName == nextByIndex && best.NextSceneName != nextByIndex))
            {
                best = ft;
            }
        }
        if (best == null && activeScene == GameTimer.FinalSceneName)
        {
            best = toMenu;
        }
        if (best == null)
        {
            return Err(cmd, "no-transition");
        }

        best.Trigger();
        if (FaintOverlay.IsRunning || best.HasTriggered)
        {
            return Ok(cmd);
        }
        return Err(cmd, "trigger-failed");
    }

    private static string NextSceneByBuildIndex()
    {
        int idx = SceneManager.GetActiveScene().buildIndex;
        if (idx < 0 || idx + 1 >= SceneManager.sceneCountInBuildSettings)
        {
            return string.Empty;
        }
        string path = SceneUtility.GetScenePathByBuildIndex(idx + 1);
        return string.IsNullOrEmpty(path) ? string.Empty : Path.GetFileNameWithoutExtension(path);
    }

    // menu: cancela la partida y vuelve al Menu con un desmayo (VR-safe); si la
    // escena no tiene ningun FaintTransition disponible, carga directo.
    private string GoToMenu()
    {
        const string cmd = "menu";
        GameTimer.AbortRun();

        if (SceneManager.GetActiveScene().name == GameTimer.MenuSceneName && !FaintOverlay.IsRunning)
        {
            return Ok(cmd);
        }
        if (!Application.CanStreamedLevelBeLoaded(GameTimer.MenuSceneName))
        {
            return Err(cmd, "no-menu-scene");
        }
        if (FaintOverlay.IsRunning)
        {
            return Err(cmd, "busy");
        }

        foreach (FaintTransition ft in FindObjectsByType<FaintTransition>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (ft != null && ft.isActiveAndEnabled && !ft.HasTriggered)
            {
                ft.TriggerToScene(GameTimer.MenuSceneName);
                if (FaintOverlay.IsRunning)
                {
                    return Ok(cmd);
                }
            }
        }

        // Sin desmayo disponible (Menu, Tutorial sin FaintTransition, etc.): carga directa.
        FaintOverlay.NotifyLoadBegin(GameTimer.MenuSceneName);
        SceneManager.LoadScene(GameTimer.MenuSceneName);
        return Ok(cmd);
    }
}
