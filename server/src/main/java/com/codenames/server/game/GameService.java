package com.codenames.server.game;

import com.codenames.server.shared.exception.GameNotFoundException;
import org.springframework.stereotype.Service;

@Service
public class GameService {

    private final GameRepository gameRepository;

    public GameService(GameRepository gameRepository) {
        this.gameRepository = gameRepository;
    }

    public Game getGameById(int gameId) {
        return gameRepository.getGameById(gameId)
            .orElseThrow(() -> new GameNotFoundException("Game not found: " + gameId));
    }
}
