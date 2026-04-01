package com.codenames.server.game.dto;

public record GameParticipantInfo(int participantId, String team, String role) {

    public boolean isSpymaster() {
        return "spymaster".equals(role);
    }

    public boolean isOperative() {
        return "operative".equals(role);
    }
}
