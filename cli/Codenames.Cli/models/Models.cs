using Codenames.Cli.Enums;

namespace Codenames.Cli.Models;

public record LobbyParticipantInfo(int UserId, string Username, bool IsHost, string? Role);

public record LobbyStateResponse(
    string LobbyId,
    string Code,
    int HostUserId,
    int MatchDurationMinutes,
    List<LobbyParticipantInfo> Participants);

public record StartGameResponse(int GameId);

public record GameParticipantIdentityResponse(int GameId, string TeamName, string RoleName);

public record GameStateResponse(int GameId, string Status, List<WordResponse> Words);

public record WordResponse(string Word, string? Category, bool Revealed);
