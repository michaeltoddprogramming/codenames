package com.codenames.server.game.dto;

public record RoundResult(int gameWordId, String word, String wordType, String outcome, int voteCount) {

    public boolean isAssassin() {
        return "assassin".equals(outcome);
    }

    public boolean isCorrect() {
        return "correct".equals(outcome);
    }
}
