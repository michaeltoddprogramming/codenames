package com.codenames.server.lobby;

import org.springframework.stereotype.Repository;

import java.security.SecureRandom;
import java.time.Duration;
import java.time.Instant;
import java.util.ArrayList;
import java.util.List;
import java.util.Optional;
import java.util.UUID;
import java.util.concurrent.ConcurrentHashMap;

@Repository
public class LobbyRepository {

    private static final String CODE_CHARS = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    private static final int CODE_LENGTH = 6;

    private final ConcurrentHashMap<String, Lobby> byId = new ConcurrentHashMap<>();
    private final ConcurrentHashMap<String, Lobby> byCode = new ConcurrentHashMap<>();
    private final SecureRandom random = new SecureRandom();

    public Lobby create(int hostUserId, String hostUsername, String hostEmail) {
        String lobbyId = UUID.randomUUID().toString();
        String code = generateUniqueCode();
        Lobby lobby = new Lobby(lobbyId, code, hostUserId, hostUsername, hostEmail);
        byId.put(lobbyId, lobby);
        byCode.put(code, lobby);
        return lobby;
    }

    public Optional<Lobby> findById(String lobbyId) {
        return Optional.ofNullable(byId.get(lobbyId));
    }

    public Optional<Lobby> findByCode(String code) {
        return Optional.ofNullable(byCode.get(code.toUpperCase()));
    }

    public Optional<Lobby> findByUserId(int userId) {
        return byId.values().stream()
                .filter(lobby -> lobby.hasParticipant(userId))
                .findFirst();
    }

    public void remove(String lobbyId) {
        Lobby lobby = byId.remove(lobbyId);
        if (lobby != null) {
            byCode.remove(lobby.code());
        }
    }

    public List<String> removeExpired(Duration maxAge) {
        Instant cutoff = Instant.now().minus(maxAge);
        List<String> removed = new ArrayList<>();
        for (Lobby lobby : byId.values()) {
            if (lobby.createdAt().isBefore(cutoff)) {
                remove(lobby.lobbyId());
                removed.add(lobby.lobbyId());
            }
        }
        return removed;
    }

    private String generateUniqueCode() {
        String code;
        do {
            code = generateCode();
        } while (byCode.containsKey(code));
        return code;
    }

    private String generateCode() {
        StringBuilder sb = new StringBuilder(CODE_LENGTH);
        for (int i = 0; i < CODE_LENGTH; i++) {
            sb.append(CODE_CHARS.charAt(random.nextInt(CODE_CHARS.length())));
        }
        return sb.toString();
    }
}
