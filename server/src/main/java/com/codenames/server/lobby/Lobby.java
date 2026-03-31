package com.codenames.server.lobby;

import java.time.Instant;
import java.util.Collection;
import java.util.concurrent.ConcurrentHashMap;

public class Lobby {

    private final String lobbyId;
    private final String code;
    private volatile int hostUserId;
    private final int playersPerTeam;
    private final int matchDurationMinutes;
    private final ConcurrentHashMap<Integer, LobbyParticipant> participants = new ConcurrentHashMap<>();
    private final Instant createdAt = Instant.now();

    public Lobby(String lobbyId, String code, int hostUserId, String hostUsername,
                 String hostEmail, int playersPerTeam, int matchDurationMinutes) {
        this.lobbyId = lobbyId;
        this.code = code;
        this.hostUserId = hostUserId;
        this.playersPerTeam = playersPerTeam;
        this.matchDurationMinutes = matchDurationMinutes;
        participants.put(hostUserId, new LobbyParticipant(hostUserId, hostUsername, hostEmail));
    }

    public String lobbyId()           { return lobbyId; }
    public String code()              { return code; }
    public int hostUserId()           { return hostUserId; }
    public int playersPerTeam()       { return playersPerTeam; }
    public int matchDurationMinutes() { return matchDurationMinutes; }
    public Instant createdAt()        { return createdAt; }

    public Collection<LobbyParticipant> participants() {
        return participants.values();
    }

    public boolean hasParticipant(int userId) {
        return participants.containsKey(userId);
    }

    /**
     * Atomically adds a participant only if the lobby is not full and they are not already in it.
     * @return true if the participant was added, false if the lobby is full or they were already present.
     */
    public synchronized boolean tryAddParticipant(LobbyParticipant participant, int maxPlayers) {
        if (participants.size() >= maxPlayers || participants.containsKey(participant.userId())) {
            return false;
        }
        participants.put(participant.userId(), participant);
        return true;
    }

    public boolean removeParticipant(int userId) {
        return participants.remove(userId) != null;
    }

    public boolean isEmpty() {
        return participants.isEmpty();
    }

    public void transferHost(int newHostUserId) {
        this.hostUserId = newHostUserId;
    }
}
