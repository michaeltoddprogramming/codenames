package com.codenames.server.game;

import com.codenames.server.game.dto.GameWord;
import java.util.List;

public class Game {

    private final int gameId;
    private final List<GameWord> words;
    private final GameStatus status;

    public Game(int gameId, List<GameWord> words, GameStatus status) {
        this.gameId = gameId;
        this.words = words;
        this.status = status;
    }

    public int gameId() {
        return gameId;
    }

    public List<GameWord> words() {
        return words;
    }

    public GameStatus status() {
        return status;
    }

    public enum GameStatus {
        ACTIVE,
        FINISHED
    }
}
