package com.codenames.server.lobby;

import com.codenames.server.lobby.dto.CreateLobbyRequest;
import com.codenames.server.lobby.dto.LobbyStateResponse;
import com.codenames.server.game.dto.StartGameResponse;
import com.codenames.server.shared.sse.SseBroadcaster;
import com.codenames.server.shared.sse.SseEmitterRegistry;
import com.codenames.server.user.User;
import org.springframework.http.ResponseEntity;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.web.bind.annotation.*;
import org.springframework.web.servlet.mvc.method.annotation.SseEmitter;

import java.io.IOException;

@RestController
@RequestMapping("/api/lobbies")
public class LobbyController {

    private final LobbyService lobbyService;
    private final SseEmitterRegistry sseEmitterRegistry;
    private final SseBroadcaster sseBroadcaster;

    public LobbyController(LobbyService lobbyService,
                           SseEmitterRegistry sseEmitterRegistry,
                           SseBroadcaster sseBroadcaster) {
        this.lobbyService = lobbyService;
        this.sseEmitterRegistry = sseEmitterRegistry;
        this.sseBroadcaster = sseBroadcaster;
    }

    @PostMapping
    public ResponseEntity<LobbyStateResponse> createLobby(
            @RequestBody CreateLobbyRequest request,
            @AuthenticationPrincipal User user) {
        Lobby lobby = lobbyService.createLobby(request, user);
        return ResponseEntity.ok(LobbyStateResponse.from(lobby));
    }

    @GetMapping("/{code}")
    public ResponseEntity<LobbyStateResponse> getLobby(@PathVariable String code) {
        Lobby lobby = lobbyService.getLobbyByCode(code);
        return ResponseEntity.ok(LobbyStateResponse.from(lobby));
    }

    @PostMapping("/{code}/join")
    public ResponseEntity<LobbyStateResponse> joinLobby(
            @PathVariable String code,
            @AuthenticationPrincipal User user) {
        Lobby lobby = lobbyService.joinLobby(code, user);
        return ResponseEntity.ok(LobbyStateResponse.from(lobby));
    }

    @PostMapping("/{lobbyId}/leave")
    public ResponseEntity<Void> leaveLobby(
            @PathVariable String lobbyId,
            @AuthenticationPrincipal User user) {
        lobbyService.leaveLobby(lobbyId, user);
        return ResponseEntity.ok().build();
    }

    @PostMapping("/{lobbyId}/start")
    public ResponseEntity<StartGameResponse> startGame(
            @PathVariable String lobbyId,
            @AuthenticationPrincipal User user) {
        int gameId = lobbyService.startGame(lobbyId, user);
        return ResponseEntity.ok(new StartGameResponse(gameId));
    }

    @GetMapping("/{lobbyId}/events")
    public SseEmitter subscribe(
            @PathVariable String lobbyId,
            @AuthenticationPrincipal User user) {
        lobbyService.ensureParticipant(lobbyId, user.userId());
        SseEmitter emitter = sseEmitterRegistry.register(lobbyId);
        LobbyStateResponse snapshot = lobbyService.getLobbySnapshot(lobbyId);
        try {
            sseBroadcaster.send(emitter, LobbyEventType.LOBBY_SNAPSHOT.name(), snapshot);
        } catch (IOException e) {
            sseEmitterRegistry.deregister(lobbyId, emitter);
            emitter.completeWithError(e);
        }
        return emitter;
    }
}
