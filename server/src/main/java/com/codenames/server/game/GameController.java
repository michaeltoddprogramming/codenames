package com.codenames.server.game;

import com.codenames.server.game.dto.GameParticipantIdentityResponse;
import com.codenames.server.game.dto.GameStateResponse;
import com.codenames.server.user.User;
import org.springframework.http.ResponseEntity;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.web.bind.annotation.*;

@RestController
@RequestMapping("/api/games")
public class GameController {

    private final GameService gameService;
    private final GameRepository gameRepository;

    public GameController(GameService gameService, GameRepository gameRepository) {
        this.gameService = gameService;
        this.gameRepository = gameRepository;
    }

    @GetMapping("/{gameId}")
    public ResponseEntity<GameStateResponse> getGame(@PathVariable int gameId) {
        Game game = gameService.getGameById(gameId);
        return ResponseEntity.ok(GameStateResponse.from(game));
    }

    @GetMapping("/{gameId}/me")
    public ResponseEntity<GameParticipantIdentityResponse> getMyIdentity(
            @PathVariable int gameId,
            @AuthenticationPrincipal User user) {
        GameParticipantIdentityResponse identity = gameRepository.getParticipantIdentity(gameId, user.userId());
        if (identity == null) {
            throw new IllegalArgumentException("You are not a participant in game " + gameId);
        }
        return ResponseEntity.ok(identity);
    }
}
