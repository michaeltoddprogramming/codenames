namespace Codenames.Cli.Models;

public record LobbyParticipantInfo(int UserId, string Username, bool IsHost);

public record LobbyStateResponse(
	string LobbyId,
	string Code,
	int HostUserId,
	int PlayersPerTeam,
	int MatchDurationMinutes,
	List<LobbyParticipantInfo> Participants);

public record StartGameResponse(int GameId);

public record GameParticipantIdentityResponse(int GameId, string TeamName, string RoleName);

public record WordCard(string  Word, string  Category, bool Revealed);

public record GameStateResponse(int GameId, string Status, string CurrentTeam, string?  ClueWord, int? ClueNumber, List<WordCard>  Board, int RedScore, int BlueScore);
