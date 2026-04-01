package com.codenames.server.lobby;

import java.time.Instant;
import java.util.Collection;
import java.util.concurrent.ConcurrentHashMap;

public class Lobby {

    private final String lobbyId;
    private final String code;
    private volatile int hostUserId;
    private final ConcurrentHashMap<Integer, LobbyParticipant> participants = new ConcurrentHashMap<>();
    private final Instant createdAt = Instant.now();
    private volatile Instant lastActivityAt = Instant.now();

    public Lobby(String lobbyId, String code, int hostUserId, String hostUsername,
                 String hostEmail) {
        this.lobbyId = lobbyId;
        this.code = code;
        this.hostUserId = hostUserId;
        participants.put(hostUserId, new LobbyParticipant(hostUserId, hostUsername, hostEmail));
    }

    public String lobbyId()           { return lobbyId; }
    public String code()              { return code; }
    public int hostUserId()           { return hostUserId; }
    public Instant createdAt()        { return createdAt; }
    public Instant lastActivityAt()   { return lastActivityAt; }

    public void touch() {
        this.lastActivityAt = Instant.now();
    }

    public Collection<LobbyParticipant> participants() {
        return participants.values();
    }

    public boolean hasParticipant(int userId) {
        return participants.containsKey(userId);
    }

    /**
     * Atomically adds a participant only if they are not already in the lobby.
     * @return true if the participant was added, false if they were already present.
     */
    public synchronized boolean tryAddParticipant(LobbyParticipant participant) {
        if (participants.containsKey(participant.userId())) {
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
