using Codenames.Cli.Models;

namespace Codenames.Cli.Lobby;

public class LobbySession
{
    private readonly object _sync = new();

    private LobbyStateResponse? _currentLobby;
    private bool _isHost;
    private int? _currentUserId;
    private int? _currentGameId;
    private GameEndResult? _currentGameResult;

    public LobbyStateResponse? CurrentLobby { get { lock (_sync) return _currentLobby; } }
    public bool IsHost { get { lock (_sync) return _isHost; } }
    public int? CurrentUserId { get { lock (_sync) return _currentUserId; } }
    public int? CurrentGameId { get { lock (_sync) return _currentGameId; } }
    public GameEndResult? CurrentGameResult { get { lock (_sync) return _currentGameResult; } }

    public void SetLobby(LobbyStateResponse lobby, int currentUserId)
    {
        lock (_sync)
        {
            _currentLobby = lobby;
            _isHost = lobby.HostUserId == currentUserId;
            _currentUserId = currentUserId;
            _currentGameId = null;
            _currentGameResult = null;
        }
    }

    public void SetGameId(int gameId)
    {
        lock (_sync)
        {
            _currentGameId = gameId;
        }
    }

    public void SetGameResult(GameEndResult result)
    {
        lock (_sync)
        {
            _currentGameResult = result;
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            _currentLobby = null;
            _isHost = false;
            _currentUserId = null;
            _currentGameId = null;
            _currentGameResult = null;
        }
    }
}
