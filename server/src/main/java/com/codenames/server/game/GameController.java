package com.codenames.server.game;

import com.codenames.server.game.dto.ClueRequest;
import com.codenames.server.game.dto.GameParticipantIdentityResponse;
import com.codenames.server.game.dto.GamePlayerResponse;
import com.codenames.server.game.dto.GameStateDetailResponse;
import com.codenames.server.game.dto.GameStateResponse;
import com.codenames.server.game.dto.VoteRequest;
import com.codenames.server.shared.sse.SseBroadcaster;
import com.codenames.server.shared.sse.SseEmitterRegistry;
import com.codenames.server.user.User;
import org.springframework.http.MediaType;
import org.springframework.http.ResponseEntity;
import java.util.List;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;
import org.springframework.web.servlet.mvc.method.annotation.SseEmitter;

@RestController
@RequestMapping("/api/games")
public class GameController {

    private final GameService gameService;
    private final GameRepository gameRepository;
    private final SseEmitterRegistry sseEmitterRegistry;
    private final SseBroadcaster sseBroadcaster;

    public GameController(
            GameService gameService,
            GameRepository gameRepository,
            SseEmitterRegistry sseEmitterRegistry,
            SseBroadcaster sseBroadcaster) {
        this.gameService = gameService;
        this.gameRepository = gameRepository;
        this.sseEmitterRegistry = sseEmitterRegistry;
        this.sseBroadcaster = sseBroadcaster;
    }

    @GetMapping("/{gameId}")
    public ResponseEntity<GameStateResponse> getGame(
            @PathVariable int gameId,
            @AuthenticationPrincipal User user) {
        Game game = gameService.getGameById(gameId);
        return ResponseEntity.ok(GameStateResponse.from(game));
    }

    @GetMapping("/{gameId}/players")
    public ResponseEntity<List<GamePlayerResponse>> getPlayers(
            @PathVariable int gameId,
            @AuthenticationPrincipal User user) {
        return ResponseEntity.ok(gameService.getGamePlayers(gameId, user));
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

    @GetMapping("/{gameId}/state")
    public ResponseEntity<GameStateDetailResponse> getGameState(
            @PathVariable int gameId,
            @AuthenticationPrincipal User user) {
        return ResponseEntity.ok(gameService.buildGameState(gameId, user));
    }

    @PostMapping("/{gameId}/clue")
    public ResponseEntity<Void> submitClue(
            @PathVariable int gameId,
            @AuthenticationPrincipal User user,
            @RequestBody ClueRequest request) {
        gameService.submitClue(gameId, user, request);
        return ResponseEntity.ok().build();
    }

    @PostMapping("/{gameId}/vote")
    public ResponseEntity<Void> submitVote(
            @PathVariable int gameId,
            @AuthenticationPrincipal User user,
            @RequestBody VoteRequest request) {
        gameService.submitVote(gameId, user, request);
        return ResponseEntity.ok().build();
    }

    @GetMapping(value = "/{gameId}/events", produces = MediaType.TEXT_EVENT_STREAM_VALUE)
    public SseEmitter subscribeToGame(
            @PathVariable int gameId,
            @AuthenticationPrincipal User user) {
        String channelId = "game-" + gameId;
        SseEmitter emitter = sseEmitterRegistry.register(channelId);

        try {
            GameStateDetailResponse snapshot = gameService.buildGameState(gameId, user);
            sseBroadcaster.send(emitter, GameEventType.GAME_SNAPSHOT.name(), snapshot);
        } catch (Exception e) {
            emitter.completeWithError(e);
        }

        return emitter;
    }
}
