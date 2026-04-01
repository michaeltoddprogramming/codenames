namespace Codenames.Cli.Enums;

public enum SseEventType
{
    GAME_SNAPSHOT,
    CLUE_GIVEN,
    VOTE_CAST,
    WORDS_REVEALED,
    ROUND_STARTED,
    TURN_SKIPPED,
    TIMER_TICK,
    GAME_ENDED,
    HEARTBEAT,
    CLUE_TIMER_STARTED,
    VOTE_TIMER_STARTED,
}
