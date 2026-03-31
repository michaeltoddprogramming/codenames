package com.codenames.server.game.dto;

public record GameWord(
    int wordId,
    String word,
    String category,
    boolean revealed
) {
    public static final String CATEGORY_RED = "red";
    public static final String CATEGORY_BLUE = "blue";
    public static final String CATEGORY_NEUTRAL = "neutral";
    public static final String CATEGORY_ASSASSIN = "assassin";
}
