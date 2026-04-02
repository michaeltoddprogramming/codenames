package com.codenames.server.game;

import org.springframework.stereotype.Component;

import java.util.concurrent.ConcurrentHashMap;

@Component
public class GameLockProvider {
    private final ConcurrentHashMap<Integer, Object> locks = new ConcurrentHashMap<>();

    public Object get(int gameId) {
        return locks.computeIfAbsent(gameId, k -> new Object());
    }
}
