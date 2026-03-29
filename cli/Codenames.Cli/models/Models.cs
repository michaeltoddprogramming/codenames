namespace Codenames.Cli.Models;

public record LobbyResponse(int Id, string Name, string JoinCode, string Status, int HostId,List<ParticipantResponse> Participants);

public record ParticipantResponse(int UserId, string Username, string? Team, string? Role);

public record WordCard(string  Word, string  Category, bool Revealed);

public record GameStateResponse(int GameId, string Status, string CurrentTeam, string?  ClueWord, int? ClueNumber, List<WordCard>  Board, int RedScore, int BlueScore);
