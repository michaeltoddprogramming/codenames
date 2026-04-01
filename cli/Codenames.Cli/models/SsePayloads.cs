namespace Codenames.Cli.Models;

public record ClueGivenPayload(string Team, string ClueWord, int ClueNumber);

public record VoteCastPayload(string Team, string Word, int RoundId);

public record WordsRevealedPayload(string Team, List<RevealedWordItem> Words);
public record RevealedWordItem(string Word, string WordType, string Outcome, int VoteCount);

public record TeamPayload(string Team);

public record TurnSkippedPayload(string Team, string Reason);

public record TimerTickPayload(long SecondsRemaining);

public record GameEndedPayload(string? Winner, string Reason, int RedRemaining, int BlueRemaining);

public record RoundTimerStartedPayload(string Team, long EndsAtEpochMs, int DurationSeconds);
