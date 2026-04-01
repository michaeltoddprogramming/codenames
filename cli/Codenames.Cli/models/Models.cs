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

public record GameStateResponse(int GameId, string Status, List<WordResponse> Words, List<ClueResponse>? Clues = null, string? CurrentClueWord = null, int? CurrentClueNumber = null);

public record WordResponse(int Id, string Word, string? Category, bool Revealed);

public record ClueResponse(string Word, int Number, string TeamName, string SubmittedBy);

public record GameStateDetailResponse(
    int GameId,
    string Status,
    DateTimeOffset MatchEndsAt,
    int RedRemaining,
    int BlueRemaining,
    string MyTeam,
    string MyRole,
    List<WordDetailView> Words,
    Dictionary<string, ActiveRoundDetailView?> ActiveRounds);

public record WordDetailView(string Word, string? Category, bool Revealed);

public record ActiveRoundDetailView(
    int RoundId,
    string ClueWord,
    int ClueNumber,
    Dictionary<string, int> Votes);

public record GameEndResult(
    string? Winner,
    string Reason,
    int RedRemaining,
    int BlueRemaining,
    string MyTeam);

public record GamePlayerInfo(string Username, string TeamName, string RoleName);
