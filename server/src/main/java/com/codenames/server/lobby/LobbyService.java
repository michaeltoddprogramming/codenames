package com.codenames.server.lobby;

import com.codenames.server.game.ClueTimerService;
import com.codenames.server.game.GameEventType;
import com.codenames.server.game.GameRepository;
import com.codenames.server.game.MatchTimerService;
import com.codenames.server.game.WordBank;
import com.codenames.server.game.dto.GameWord;
import com.codenames.server.lobby.dto.LobbyStateResponse;
import com.codenames.server.shared.exception.LobbyNotFoundException;
import com.codenames.server.shared.sse.SseBroadcaster;
import com.codenames.server.shared.sse.SseEmitterRegistry;
import com.codenames.server.user.User;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.scheduling.annotation.Scheduled;
import org.springframework.stereotype.Service;

import java.time.Duration;
import java.util.ArrayList;
import java.util.Collections;
import java.util.List;
import java.util.Map;
import java.util.Optional;
import java.util.concurrent.ThreadLocalRandom;

@Service
public class LobbyService {

    private static final Logger logger = LoggerFactory.getLogger(LobbyService.class);
    private static final Duration LOBBY_MAX_AGE = Duration.ofMinutes(30);

    private final LobbyRepository lobbyRepository;
    private final GameRepository gameRepository;
    private final SseBroadcaster sseBroadcaster;
    private final SseEmitterRegistry sseEmitterRegistry;
    private final WordBank wordBank;
    private final ClueTimerService clueTimerService;
    private final MatchTimerService matchTimerService;
    private final int matchDurationMinutes;

    public LobbyService(LobbyRepository lobbyRepository,
                        GameRepository gameRepository,
                        SseBroadcaster sseBroadcaster,
                        SseEmitterRegistry sseEmitterRegistry,
                        WordBank wordBank,
                        @Value("${app.game.match-duration-minutes:5}") int matchDurationMinutes,
                        ClueTimerService clueTimerService,
                        MatchTimerService matchTimerService) {
        this.lobbyRepository = lobbyRepository;
        this.gameRepository = gameRepository;
        this.sseBroadcaster = sseBroadcaster;
        this.sseEmitterRegistry = sseEmitterRegistry;
        this.wordBank = wordBank;
        this.clueTimerService = clueTimerService;
        this.matchTimerService = matchTimerService;
        this.matchDurationMinutes = matchDurationMinutes;
    }

    public Lobby createLobby(User user) {
        ensureNotInAnyLobby(user.userId());
        return lobbyRepository.create(user.userId(), user.username(), user.email());
    }

    public Lobby getLobbyByCode(String code) {
        return lobbyRepository.findByCode(code)
                .orElseThrow(() -> new LobbyNotFoundException("Lobby not found: " + code));
    }

    public Lobby getLobbyById(String lobbyId) {
        return lobbyRepository.findById(lobbyId)
                .orElseThrow(() -> new LobbyNotFoundException("Lobby not found: " + lobbyId));
    }

    public Lobby joinLobby(String code, User user) {
        ensureNotInAnyLobby(user.userId());
        Lobby lobby = getLobbyByCode(code);
        boolean added = lobby.tryAddParticipant(
                new LobbyParticipant(user.userId(), user.username(), user.email()));
        if (!added) {
            throw new IllegalStateException("You are already in this lobby");
        }
        sseBroadcaster.broadcast(lobby.lobbyId(), LobbyEventType.PLAYER_JOINED.name(),
                Map.of("userId", user.userId(), "username", user.username()));
        sseBroadcaster.broadcast(lobby.lobbyId(), LobbyEventType.LOBBY_SNAPSHOT.name(),
                LobbyStateResponse.from(lobby, matchDurationMinutes));
        return lobby;
    }

    public void leaveLobby(String lobbyId, User user) {
        Lobby lobby = getLobbyById(lobbyId);
        if (!lobby.hasParticipant(user.userId())) {
            throw new IllegalStateException("You are not in this lobby");
        }

        lobby.removeParticipant(user.userId());

        if (lobby.isEmpty()) {
            lobbyRepository.remove(lobbyId);
            sseEmitterRegistry.removeChannel(lobbyId);
            return;
        }

        if (lobby.hostUserId() == user.userId()) {
            LobbyParticipant newHost = lobby.participants().iterator().next();
            lobby.transferHost(newHost.userId());
        }

        sseBroadcaster.broadcast(lobbyId, LobbyEventType.PLAYER_LEFT.name(),
                Map.of("userId", user.userId(), "username", user.username()));
        sseBroadcaster.broadcast(lobbyId, LobbyEventType.LOBBY_SNAPSHOT.name(),
            LobbyStateResponse.from(lobby, matchDurationMinutes));
    }

    public void ensureParticipant(String lobbyId, int userId) {
        Lobby lobby = getLobbyById(lobbyId);
        if (!lobby.hasParticipant(userId)) {
            throw new IllegalStateException("You are not a participant in this lobby");
        }
    }

    public int startGame(String lobbyId, User user) {
        Lobby lobby = getLobbyById(lobbyId);
        if (lobby.hostUserId() != user.userId()) {
            throw new IllegalStateException("Only the host can start the game");
        }

        if (lobby.participants().size() < 4) {
            throw new IllegalStateException("At least 4 players are required to start the game");
        }

        List<String> words = wordBank.selectRandom(25);
        List<Map.Entry<String, Integer>> categoryConfig = List.of(
            Map.entry(GameWord.CATEGORY_RED,      8),
            Map.entry(GameWord.CATEGORY_BLUE,     8),
            Map.entry(GameWord.CATEGORY_NEUTRAL,  8),
            Map.entry(GameWord.CATEGORY_ASSASSIN, 1)
        );

        List<Map<String, String>> wordList = new ArrayList<>();
        int index = 0;
        for (var entry : categoryConfig) {
            for (int i = 0; i < entry.getValue(); i++) {
                wordList.add(Map.of("word", words.get(index++), "word_type", entry.getKey()));
            }
        }

        int gameId = gameRepository.createGame(wordList, matchDurationMinutes);
        List<Map<String, Object>> participants = assignTeamsAndRoles(lobby);
        gameRepository.insertParticipants(gameId, participants);

        sseBroadcaster.broadcast(lobbyId, LobbyEventType.GAME_STARTED.name(), Map.of("gameId", gameId));
        lobbyRepository.remove(lobbyId);
        sseEmitterRegistry.removeChannel(lobbyId);

        clueTimerService.start(gameId, "red");
        clueTimerService.start(gameId, "blue");
        matchTimerService.start(gameId, matchDurationMinutes);

        sseBroadcaster.broadcast("game-" + gameId, GameEventType.ROUND_STARTED.name(), Map.of("team", "red"));
        sseBroadcaster.broadcast("game-" + gameId, GameEventType.ROUND_STARTED.name(), Map.of("team", "blue"));

        return gameId;
    }

    public Optional<LobbyStateResponse> findMyLobby(User user) {
        return lobbyRepository.findByUserId(user.userId())
                .map(lobby -> LobbyStateResponse.from(lobby, matchDurationMinutes));
    }

    public LobbyStateResponse getLobbySnapshot(String lobbyId) {
        return LobbyStateResponse.from(getLobbyById(lobbyId), matchDurationMinutes);
    }

    @Scheduled(fixedDelay = 60_000)
    public void cleanupExpiredLobbies() {
        List<String> removed = lobbyRepository.removeExpired(LOBBY_MAX_AGE);
        for (String lobbyId : removed) {
            sseEmitterRegistry.removeChannel(lobbyId);
            logger.info("Removed expired lobby {}", lobbyId);
        }
    }

    private void ensureNotInAnyLobby(int userId) {
        lobbyRepository.findByUserId(userId).ifPresent(existing -> {
            throw new IllegalStateException(
                    "You are already in lobby " + existing.code() + ". Leave it before joining or creating another.");
        });
    }

    private List<Map<String, Object>> assignTeamsAndRoles(Lobby lobby) {
        if (lobby.participants().size() < 4) {
            throw new IllegalStateException("Need at least 4 players to start the game.");
        }

        ThreadLocalRandom rng = ThreadLocalRandom.current();
        List<LobbyParticipant> shuffled = new ArrayList<>(lobby.participants());
        Collections.shuffle(shuffled, rng);

        int half = shuffled.size() / 2;
        List<LobbyParticipant> redTeam  = new ArrayList<>(shuffled.subList(0, half));
        List<LobbyParticipant> blueTeam = new ArrayList<>(shuffled.subList(half, shuffled.size()));

        int redSpymasterUserId  = redTeam.get(rng.nextInt(redTeam.size())).userId();
        int blueSpymasterUserId = blueTeam.get(rng.nextInt(blueTeam.size())).userId();

        List<Map<String, Object>> assignments = new ArrayList<>();
        for (LobbyParticipant p : redTeam) {
            assignments.add(Map.of(
                    "user_id",   p.userId(),
                    "team_name", "red",
                    "role_name", p.userId() == redSpymasterUserId ? "spymaster" : "operative"
            ));
        }
        for (LobbyParticipant p : blueTeam) {
            assignments.add(Map.of(
                    "user_id",   p.userId(),
                    "team_name", "blue",
                    "role_name", p.userId() == blueSpymasterUserId ? "spymaster" : "operative"
            ));
        }
        return assignments;
    }
}
