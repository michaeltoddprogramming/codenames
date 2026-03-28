package com.codenames.server.user;

import org.springframework.stereotype.Component;

import java.util.UUID;
import java.util.concurrent.ConcurrentHashMap;

@Component
public class UserStore {

    private final ConcurrentHashMap<String, User> byOauthSub = new ConcurrentHashMap<>();

    public User findOrCreate(String sub, String email, String name) {
        return byOauthSub.computeIfAbsent(sub,
                key -> new User(UUID.randomUUID().toString(), email, name, sub));
    }
}
