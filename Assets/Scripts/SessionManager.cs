using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.SinglePlayer;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

// Handles playing over the internet. It works like this:
//   1. Everyone signs in to Unity's services anonymously (no account needed).
//   2. The host creates a "session" and gets a short join code.
//   3. Friends type the code. Unity Relay passes the traffic between you, so
//      nobody has to open ports on their router.
// Once the session exists, Netcode starts by itself and spawns the players.
// This class has no visuals; GameUI draws the menu and calls into it.
public class SessionManager : MonoBehaviour
{
    const int MaxPlayers = 4;

    public static SessionManager Instance { get; private set; }

    public ISession Session { get; private set; }

    // Playing alone needs no internet: the game simply hosts itself.
    public bool Solo { get; private set; }
    public bool InGame => Session != null || Solo;
    public bool Ready { get; private set; }
    public bool Busy { get; private set; }
    public string Status { get; private set; } = "Connecting...";

    void Awake()
    {
        Instance = this;
    }

    async void Start()
    {
        try
        {
            // A profile is a separate saved identity. Two copies of the game on
            // one computer need different profiles or they count as one player.
            var options = new InitializationOptions();
            options.SetProfile(ArgValue("-profile") ?? (Application.isEditor ? "editor" : "player"));
            await UnityServices.InitializeAsync(options);

            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();

            Ready = true;
            Status = "";
        }
        catch (Exception e)
        {
            Status = "Can't reach the online service. Check your internet.";
            Debug.LogException(e);
            return;
        }

        // Lets a built game skip the menu: "-solo", "-host" or "-join <code>".
        if (HasArg("-solo")) PlaySolo();
        else if (HasArg("-host")) await Host();
        else if (ArgValue("-join") is string code) await Join(code);
    }

    public void PlaySolo()
    {
        if (Busy || InGame) return;
        var network = NetworkManager.Singleton;
        // A solo game talks to nobody, so it uses a transport with no network
        // in it at all. That also lets it run in a web browser.
        network.NetworkConfig.NetworkTransport = network.GetComponent<SinglePlayerTransport>();
        Solo = network.StartHost();
        Status = Solo ? "" : "Could not start the game.";
    }

    // Online games go through Unity's transport. Browsers can only use WebSockets.
    static void UseOnlineTransport()
    {
        var network = NetworkManager.Singleton;
        var transport = network.GetComponent<UnityTransport>();
#if UNITY_WEBGL
        // (Also true in the Editor while its build target is set to Web.)
        transport.UseWebSockets = true;
#else
        transport.UseWebSockets = false;
#endif
        network.NetworkConfig.NetworkTransport = transport;
    }

    public Task Host()
    {
        return Run("Creating game...", async () =>
        {
            UseOnlineTransport();
            var options = new SessionOptions { MaxPlayers = MaxPlayers }.WithRelayNetwork();
            Watch(await MultiplayerService.Instance.CreateSessionAsync(options));
            Debug.Log("JOIN CODE: " + Session.Code);
        });
    }

    public Task Join(string code)
    {
        return Run("Joining...", async () =>
        {
            UseOnlineTransport();
            Watch(await MultiplayerService.Instance.JoinSessionByCodeAsync(code.Trim().ToUpperInvariant()));
        });
    }

    // If the host quits or we get dropped, go back to the title screen.
    void Watch(ISession session)
    {
        Session = session;
        session.RemovedFromSession += () => Ended(session, "You were removed from the game.");
        session.Deleted += () => Ended(session, "The game ended: the host left. Your progress is saved.");
    }

    void Ended(ISession session, string message)
    {
        if (Session != session) return;
        Session = null;
        Status = message;
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) NetworkManager.Singleton.Shutdown();
    }

    float lostFor;

    void Update()
    {
        // If the host quits or the connection drops, the network stops but the
        // session lingers. Notice that and go back to the title screen with a
        // message, instead of leaving the player staring at a frozen town.
        var network = NetworkManager.Singleton;
        bool lost = Session != null && !Busy && network != null && !network.IsListening;
        lostFor = lost ? lostFor + Time.unscaledDeltaTime : 0f;
        if (lostFor < 1.5f) return;

        lostFor = 0f;
        var gone = Session;
        Session = null;
        Status = "The game ended: the host left. Your progress is saved.";
        Abandon(gone);
    }

    static async void Abandon(ISession session)
    {
        try { await session.LeaveAsync(); }
        catch (Exception) { /* it was already gone */ }
    }

    public Task Leave()
    {
        if (Solo)
        {
            NetworkManager.Singleton.Shutdown();
            Solo = false;
            return Task.CompletedTask;
        }
        return Run("Leaving...", async () =>
        {
            var leaving = Session;
            Session = null;
            if (leaving != null) await leaving.LeaveAsync();
        });
    }

    async Task Run(string message, Func<Task> action)
    {
        if (Busy || !Ready) return;
        Busy = true;
        Status = message;
        try
        {
            await action();
            Status = "";
        }
        catch (Exception e)
        {
            Status = Friendly(e);
            Debug.LogException(e);
        }
        Busy = false;
    }

    static string Friendly(Exception e)
    {
        string message = e.Message.ToLowerInvariant();
        if (message.Contains("not found") || message.Contains("invalid")) return "No game with that code.";
        if (message.Contains("full")) return "That game is full.";
        return "Something went wrong. Try again.";
    }

    static bool HasArg(string name) => Array.IndexOf(Environment.GetCommandLineArgs(), name) >= 0;

    static string ArgValue(string name)
    {
        var args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
