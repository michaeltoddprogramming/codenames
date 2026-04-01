package com.codenames.server.game.dto;

import java.time.Instant;
import java.util.List;
import java.util.Map;

public record GameStateDetailResponse(
        int gameId,
        String status,
        Instant matchEndsAt,
        int redRemaining,
        int blueRemaining,
        String myTeam,
        String myRole,
        List<WordView> words,
        Map<String, ActiveRoundView> activeRounds,
        RoundTimerView roundTimer
) {

    public record WordView(String word, String category, boolean revealed) {}

    public record ActiveRoundView(int roundId, String clueWord, int clueNumber, Map<String, Long> votes) {}

    public record RoundTimerView(String type, String team, long endsAtEpochMs, int durationSeconds) {}
}
