package com.codenames.server.lobby.dto;

import com.codenames.server.lobby.Lobby;

import java.util.List;

public record LobbyStateResponse(
        String lobbyId,
        String code,
        int hostUserId,
        int matchDurationMinutes,
        List<ParticipantInfo> participants
) {

    public record ParticipantInfo(int userId, String username, boolean isHost) {
    }

        public static LobbyStateResponse from(Lobby lobby, int matchDurationMinutes) {
        List<ParticipantInfo> participantInfos = lobby.participants().stream()
                .map(p -> new ParticipantInfo(
                        p.userId(),
                        p.username(),
                        p.userId() == lobby.hostUserId()
                ))
                .toList();
        return new LobbyStateResponse(
                lobby.lobbyId(),
                lobby.code(),
                lobby.hostUserId(),
                                matchDurationMinutes,
                participantInfos
        );
    }
}
