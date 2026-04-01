using Codenames.Cli.Models;

namespace Codenames.Cli.Lobby;

public class LobbySession
{
    private readonly object _sync = new();

    public LobbyStateResponse? CurrentLobby { get; private set; }
    public bool IsHost { get; private set; }
    public int? CurrentUserId { get; private set; }
    public int? CurrentGameId { get; private set; }

    public void SetLobby(LobbyStateResponse lobby, int currentUserId)
    {
        lock (_sync)
        {
            CurrentLobby = lobby;
            IsHost = lobby.HostUserId == currentUserId;
            CurrentUserId = currentUserId;
            CurrentGameId = null;
        }
    }

    public void SetGameId(int gameId)
    {
        lock (_sync)
        {
            CurrentGameId = gameId;
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            CurrentLobby = null;
            IsHost = false;
            CurrentUserId = null;
            CurrentGameId = null;
        }
    }
}
