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
///   goto <escena> -> ok | err busy|no-scene   (salta a la escena; marca "con salto")
///   restart       -> ok | err <motivo>        (recarga la escena activa)
///   fair on|off   -> ok | err <motivo>
///   portals visible|subtle -> ok | err <motivo>
///   fps 30|60     -> ok | err <motivo>
///   bloom off|low|full, post on|off, hdr on|off, scale 0.5..1 -> ok | err bad-arg
///   status        -> status <activa 0/1> <segundos F2> fps=<n.n> target=<30|60> fair=<0|1> portals=<visible|subtle> paused=<0|1> gfx=bloom:<m>,post:<0|1>,hdr:<0|1>,scale:<f> <escena> obj=<objetivo, espacios como _ o -> hint=<escalon 0..3>
///   pause / resume -> para o reanuda el cronometro (el juego sigue)
///   scare screamer|whisper|steps|distant|knock|blackout|heartbeat -> ok | err <motivo> (cooldown 1.5 s)
///   tension 0..1  -> ok | err <motivo>
///   sfx <nombre>  -> ok | err <motivo>  (clip de la biblioteca, 2D)
///   hintlvl 1|2|3 -> ok | err <motivo>  (pista de nivel fijo del objetivo actual)
///   say <texto>   -> ok | err <motivo>  (texto en VR, hasta 120 caracteres)
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

            case "goto":
                return GoTo(rest);

            case "restart":
                return Restart();

            case "fair":
                if (rest == "on" || rest == "off")
                {
                    GameSettings.GetOrCreate().SetFairMode(rest == "on");
                    return Ok(cmd);
                }
                return Err(cmd, "bad-arg");

            case "portals":
                if (rest == "visible" || rest == "subtle")
                {
                    GameSettings.GetOrCreate().SetPortalsVisible(rest == "visible");
                    return Ok(cmd);
                }
                return Err(cmd, "bad-arg");

            case "fps":
                if (rest == "30" || rest == "60")
                {
                    GameSettings.GetOrCreate().SetTargetFps(rest == "30" ? 30 : 60);
                    return Ok(cmd);
                }
                return Err(cmd, "bad-arg");

            case "bloom":
            case "post":
            case "hdr":
            case "scale":
                return SetGraphics(cmd, rest);

            case "fx":
            {
                // Diagnóstico, no se guarda: "fx <pieza> <on|off>" (ver PostFxDebug).
                string what = FirstToken(rest, out string val);
                return PostFxDebug.Set(what, val.Trim().ToLowerInvariant()) ? Ok(cmd + " " + what + " " + val.Trim()) : Err(cmd, "bad-arg");
            }

            case "pause":
            case "resume":
                if (!GameTimer.IsRunning)
                {
                    return Err(cmd, "no-run");
                }
                GameTimer.SetOperatorPaused(cmd == "pause");
                return Ok(cmd);

            case "status":
                GameSettings gs = GameSettings.GetOrCreate();
                // obj= y hint= van al final: el panel busca esos campos en cualquier posicion.
                string objective = HintDirector.Current != null ? HintDirector.Current.CurrentObjectiveName : string.Empty;
                objective = string.IsNullOrWhiteSpace(objective) ? "-" : objective.Trim().Replace(' ', '_');
                int hintStep = HintDirector.Current != null ? HintDirector.Current.CurrentStep : 0;
                string status = "status " + (GameTimer.IsRunning ? "1" : "0") + " " +
                                GameTimer.Fmt(GameTimer.ElapsedSeconds) +
                                " fps=" + PerfMeter.CurrentFps.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) +
                                " target=" + gs.TargetFps +
                                " fair=" + (gs.FairMode ? "1" : "0") +
                                " portals=" + (gs.PortalsVisible ? "visible" : "subtle") +
                                " paused=" + (GameTimer.IsOperatorPaused ? "1" : "0") +
                                " gfx=" + GfxString(gs) + " " +
                                SceneManager.GetActiveScene().name +
                                " obj=" + objective + " hint=" + hintStep;
                return Ok(cmd, status);

            case "scare":
            {
                string kind = rest.Trim().ToLowerInvariant();
                string scareResult = OperatorScares.Scare(kind);
                return scareResult == "ok" ? Ok(cmd + " " + kind) : Err(cmd, scareResult);
            }

            case "tension":
                if (!float.TryParse(rest, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float tension))
                {
                    return Err(cmd, "bad-arg");
                }
                string tensionResult = OperatorScares.Tension(tension);
                return tensionResult == "ok" ? Ok(cmd + " " + tension.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)) : Err(cmd, tensionResult);

            case "sfx":
            {
                if (rest.Length == 0)
                {
                    return Err(cmd, "bad-arg");
                }
                string sfxResult = OperatorScares.Sfx(rest);
                return sfxResult == "ok" ? Ok(cmd + " " + rest) : Err(cmd, sfxResult);
            }

            case "hintlvl":
                if (HintDirector.Current == null)
                {
                    return Err(cmd, "no hints");
                }
                if (!int.TryParse(rest, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int hintLevel) ||
                    hintLevel < 1 || hintLevel > 3)
                {
                    return Err(cmd, "bad-arg");
                }
                HintDirector.Current.GiveHintLevel(hintLevel);
                return Ok(cmd + " " + hintLevel);

            case "say":
            {
                // El texto es el resto de la linea (conserva espacios), maximo 120 caracteres.
                string text = rest.Trim();
                if (text.Length == 0)
                {
                    return Err(cmd, "empty-text");
                }
                if (text.Length > 120)
                {
                    text = text.Substring(0, 120).TrimEnd();
                }
                string sayResult = OperatorScares.Say(text);
                return sayResult == "ok" ? Ok(cmd) : Err(cmd, sayResult);
            }

            default:
                return Err(cmd.Length > 0 ? cmd : "?", "unknown");
        }
    }

    private static string GfxString(GameSettings gs)
    {
        return "bloom:" + gs.Bloom.ToString().ToLowerInvariant() +
               ",post:" + (gs.PostProcessing ? "1" : "0") +
               ",hdr:" + (gs.Hdr ? "1" : "0") +
               ",scale:" + gs.RenderScale.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) +
               ",eye:" + UnityEngine.XR.XRSettings.eyeTextureWidth + "x" + UnityEngine.XR.XRSettings.eyeTextureHeight +
               ",api:" + SystemInfo.graphicsDeviceType;
    }

    /// <summary>bloom off|low|full, post on|off, hdr on|off, scale 0.5..1.</summary>
    private static string SetGraphics(string cmd, string arg)
    {
        GameSettings gs = GameSettings.GetOrCreate();
        GraphicsQuality.BloomMode bloom = gs.Bloom;
        bool post = gs.PostProcessing, hdr = gs.Hdr;
        float scale = gs.RenderScale;
        switch (cmd)
        {
            case "bloom":
                if (arg == "off") bloom = GraphicsQuality.BloomMode.Off;
                else if (arg == "low") bloom = GraphicsQuality.BloomMode.Low;
                else if (arg == "full") bloom = GraphicsQuality.BloomMode.Full;
                else return Err(cmd, "bad-arg");
                break;
            case "post":
            case "hdr":
                if (arg != "on" && arg != "off") return Err(cmd, "bad-arg");
                if (cmd == "post") post = arg == "on"; else hdr = arg == "on";
                break;
            default:
                if (!float.TryParse(arg, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out scale) ||
                    scale < 0.5f || scale > 1f)
                {
                    return Err(cmd, "bad-arg");
                }
                break;
        }
        gs.SetGraphics(bloom, post, hdr, scale);
        return Ok(cmd);
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

    // goto: salta a una escena por nombre exacto (puede tener espacios y tildes).
    // Si no hay partida y la escena no es el Menu, arranca una (como Empezar).
    private string GoTo(string scene)
    {
        const string cmd = "goto";
        if (scene.Length == 0 || !Application.CanStreamedLevelBeLoaded(scene))
        {
            return Err(cmd, "no-scene");
        }
        if (FaintOverlay.IsRunning)
        {
            return Err(cmd, "busy");
        }
        if (!GameTimer.IsRunning && scene != GameTimer.MenuSceneName)
        {
            GameTimer.StartRun();
        }
        // El servidor del operador marca "con salto" al ver esta linea.
        GameTimer.Log("cmd goto ok " + scene);
        LoadScene(scene);
        return "ok";
    }

    // restart: recarga la escena activa (sin marcar salto). No sirve en el Menu.
    private string Restart()
    {
        const string cmd = "restart";
        string scene = SceneManager.GetActiveScene().name;
        if (scene == GameTimer.MenuSceneName)
        {
            return Err(cmd, "in-menu");
        }
        if (FaintOverlay.IsRunning)
        {
            return Err(cmd, "busy");
        }
        GameTimer.Log("cmd restart ok");
        LoadScene(scene);
        return "ok";
    }

    // Prefiere un desmayo (VR-safe); sin ninguno disponible, carga directa.
    private static void LoadScene(string scene)
    {
        foreach (FaintTransition ft in FindObjectsByType<FaintTransition>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (ft != null && ft.isActiveAndEnabled && !ft.HasTriggered)
            {
                ft.TriggerToScene(scene);
                if (FaintOverlay.IsRunning)
                {
                    return;
                }
            }
        }
        FaintOverlay.NotifyLoadBegin(scene);
        SceneManager.LoadScene(scene);
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
