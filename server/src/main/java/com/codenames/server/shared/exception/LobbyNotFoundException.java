package com.codenames.server.shared.exception;

public class LobbyNotFoundException extends RuntimeException {

    public LobbyNotFoundException(String message) {
        super(message);
    }
}
