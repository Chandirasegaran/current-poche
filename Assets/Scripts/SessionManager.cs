using System;
using System.Threading.Tasks;
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

        // Lets a built game skip the menu: "-host" or "-join <code>".
        if (HasArg("-host")) await Host();
        else if (ArgValue("-join") is string code) await Join(code);
    }

    public Task Host()
    {
        return Run("Creating game...", async () =>
        {
            var options = new SessionOptions { MaxPlayers = MaxPlayers }.WithRelayNetwork();
            Session = await MultiplayerService.Instance.CreateSessionAsync(options);
            Debug.Log("JOIN CODE: " + Session.Code);
        });
    }

    public Task Join(string code)
    {
        return Run("Joining...", async () =>
        {
            Session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code.Trim().ToUpperInvariant());
        });
    }

    public Task Leave()
    {
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
