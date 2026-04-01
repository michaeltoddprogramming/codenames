package com.codenames.server.game;

import com.codenames.server.user.User;
import org.springframework.http.ResponseEntity;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

@RestController
@RequestMapping("/api/players")
public class PlayerController {

    private final GameService gameService;

    public PlayerController(GameService gameService) {
        this.gameService = gameService;
    }

    @GetMapping("/me/active-game")
    public ResponseEntity<ActiveGameResponse> getActiveGame(@AuthenticationPrincipal User user) {
        return gameService.getMyActiveGame(user.userId())
                .map(gameId -> ResponseEntity.ok(new ActiveGameResponse(gameId)))
                .orElse(ResponseEntity.notFound().build());
    }

    public record ActiveGameResponse(int gameId) {}
}
