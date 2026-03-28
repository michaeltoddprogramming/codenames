package com.codenames.server.auth;

public record AuthResponse(String token, String email, String name) {}
