package com.codenames.server.lobby.dto;

public record CreateLobbyRequest(int playersPerTeam, int matchDurationMinutes) {
}
