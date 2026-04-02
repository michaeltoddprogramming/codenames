package com.codenames.server.lobby;

import java.time.Instant;
import java.util.Collection;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.CopyOnWriteArrayList;

public class Lobby {

    private final String lobbyId;
    private final String code;
    private volatile int hostUserId;
    private final ConcurrentHashMap<Integer, LobbyParticipant> participants = new ConcurrentHashMap<>();
    private final CopyOnWriteArrayList<Integer> joinOrder = new CopyOnWriteArrayList<>();
    private final Instant createdAt = Instant.now();
    private volatile Instant lastActivityAt = Instant.now();

    public Lobby(String lobbyId, String code, int hostUserId, String hostUsername,
                 String hostEmail) {
        this.lobbyId = lobbyId;
        this.code = code;
        this.hostUserId = hostUserId;
        participants.put(hostUserId, new LobbyParticipant(hostUserId, hostUsername, hostEmail));
        joinOrder.add(hostUserId);
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
        joinOrder.add(participant.userId());
        return true;
    }

    public boolean removeParticipant(int userId) {
        joinOrder.remove(Integer.valueOf(userId));
        return participants.remove(userId) != null;
    }

    /** Returns the participant who has been waiting the longest (first to join after the host leaves). */
    public LobbyParticipant longestWaitingParticipant() {
        for (int userId : joinOrder) {
            LobbyParticipant p = participants.get(userId);
            if (p != null) return p;
        }
        throw new IllegalStateException("No participants left in lobby " + lobbyId);
    }

    public boolean isEmpty() {
        return participants.isEmpty();
    }

    public void transferHost(int newHostUserId) {
        this.hostUserId = newHostUserId;
    }
}
