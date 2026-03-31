package com.codenames.server.game.dto;

import com.codenames.server.game.Game;
import com.codenames.server.game.Game.GameStatus;

import java.util.List;

public record GameStateResponse(
    int gameId,
    GameStatus status,
    List<WordResponse> words
) {
    public static GameStateResponse from(Game game) {
        List<WordResponse> wordResponses = game.words().stream()
            .map(WordResponse::from)
            .toList();
        return new GameStateResponse(
            game.gameId(),
            game.status(),
            wordResponses
        );
    }

    public record WordResponse(String word, String category, boolean revealed) {
        public static WordResponse from(GameWord gameWord) {
            return new WordResponse(
                gameWord.word(),
                gameWord.category(),
                gameWord.revealed()
            );
        }
    }
}
