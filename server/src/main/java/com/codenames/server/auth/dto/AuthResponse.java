package com.codenames.server.auth.dto;

public record AuthResponse(String token, String email, String name) {
}
