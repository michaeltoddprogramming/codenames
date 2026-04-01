package com.codenames.server.game;

public enum GameEventType {
    GAME_SNAPSHOT,
    CLUE_GIVEN,
    VOTE_CAST,
    WORDS_REVEALED,
    ROUND_STARTED,
    TURN_SKIPPED,
    TIMER_TICK,
    GAME_ENDED
}
