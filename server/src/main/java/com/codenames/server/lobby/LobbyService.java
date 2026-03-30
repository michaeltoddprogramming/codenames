package com.codenames.server.lobby;

import com.codenames.server.game.GameRepository;
import com.codenames.server.game.WordBank;
import com.codenames.server.lobby.dto.CreateLobbyRequest;
import com.codenames.server.lobby.dto.LobbyStateResponse;
import com.codenames.server.shared.exception.LobbyNotFoundException;
import com.codenames.server.shared.sse.SseBroadcaster;
import com.codenames.server.user.User;
import org.springframework.stereotype.Service;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;
import java.util.Map;
import java.util.Random;
import java.util.Set;

@Service
public class LobbyService {

    private static final Set<Integer> VALID_MATCH_DURATIONS = Set.of(3, 5, 10, 15);

    private final LobbyRepository lobbyRepository;
    private final GameRepository gameRepository;
    private final SseBroadcaster sseBroadcaster;
    private final Random random = new Random();

    public LobbyService(LobbyRepository lobbyRepository,
                        GameRepository gameRepository,
                        SseBroadcaster sseBroadcaster) {
        this.lobbyRepository = lobbyRepository;
        this.gameRepository = gameRepository;
        this.sseBroadcaster = sseBroadcaster;
    }

    public Lobby createLobby(CreateLobbyRequest request, User user) {
        if (request.playersPerTeam() < 2 || request.playersPerTeam() > 5) {
            throw new IllegalArgumentException("Players per team must be between 2 and 5");
        }
        if (!VALID_MATCH_DURATIONS.contains(request.matchDurationMinutes())) {
            throw new IllegalArgumentException("Match duration must be 3, 5, 10, or 15 minutes");
        }
        return lobbyRepository.create(user.userId(), user.username(), user.email(),
                request.playersPerTeam(), request.matchDurationMinutes());
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
        Lobby lobby = getLobbyByCode(code);
        if (lobby.hasParticipant(user.userId())) {
            throw new IllegalStateException("User is already in this lobby");
        }
        int requiredTotal = lobby.playersPerTeam() * 2;
        if (lobby.participants().size() >= requiredTotal) {
            throw new IllegalStateException("Lobby is full (" + requiredTotal + " players max)");
        }
        lobby.addParticipant(new LobbyParticipant(user.userId(), user.username(), user.email()));
        sseBroadcaster.broadcast(lobby.lobbyId(), LobbyEventType.PLAYER_JOINED.name(),
                Map.of("userId", user.userId(), "username", user.username()));
        return lobby;
    }

    public int startGame(String lobbyId, User user) {
        Lobby lobby = getLobbyById(lobbyId);
        if (lobby.hostUserId() != user.userId()) {
            throw new IllegalStateException("Only the host can start the game");
        }

        int requiredTotal = lobby.playersPerTeam() * 2;
        if (lobby.participants().size() != requiredTotal) {
            throw new IllegalStateException(
                    "Exactly " + requiredTotal + " players required to start (" +
                    lobby.participants().size() + " present)");
        }

        List<String> words = WordBank.selectRandom(25);
        List<Map<String, String>> wordList = new ArrayList<>();
        for (int i = 0; i < 10; i++)  wordList.add(Map.of("word", words.get(i),  "word_type", "red"));
        for (int i = 10; i < 20; i++) wordList.add(Map.of("word", words.get(i),  "word_type", "blue"));
        for (int i = 20; i < 24; i++) wordList.add(Map.of("word", words.get(i),  "word_type", "neutral"));
        wordList.add(Map.of("word", words.get(24), "word_type", "assassin"));

        int gameId = gameRepository.createGame(wordList);
        List<Map<String, Object>> participants = assignTeamsAndRoles(lobby);
        gameRepository.insertParticipants(gameId, participants);

        sseBroadcaster.broadcast(lobbyId, LobbyEventType.GAME_STARTED.name(), Map.of("gameId", gameId));
        lobbyRepository.remove(lobbyId);
        return gameId;
    }

    public LobbyStateResponse getLobbySnapshot(String lobbyId) {
        return LobbyStateResponse.from(getLobbyById(lobbyId));
    }

    private List<Map<String, Object>> assignTeamsAndRoles(Lobby lobby) {
        List<LobbyParticipant> shuffled = new ArrayList<>(lobby.participants());
        Collections.shuffle(shuffled, random);

        int half = shuffled.size() / 2;
        List<LobbyParticipant> redTeam  = new ArrayList<>(shuffled.subList(0, half));
        List<LobbyParticipant> blueTeam = new ArrayList<>(shuffled.subList(half, shuffled.size()));

        int redSpymasterUserId  = redTeam.get(random.nextInt(redTeam.size())).userId();
        int blueSpymasterUserId = blueTeam.get(random.nextInt(blueTeam.size())).userId();

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
