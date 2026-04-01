package com.codenames.server.lobby;

import com.codenames.server.game.GameRepository;
import com.codenames.server.game.WordBank;
import com.codenames.server.lobby.dto.CreateLobbyRequest;
import com.codenames.server.shared.sse.SseBroadcaster;
import com.codenames.server.shared.sse.SseEmitterRegistry;
import com.codenames.server.user.User;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Nested;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.ArgumentCaptor;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

import java.util.ArrayList;
import java.util.List;
import java.util.Map;
import java.util.Optional;
import java.util.stream.IntStream;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.mockito.ArgumentMatchers.anyInt;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.Mockito.never;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;

@ExtendWith(MockitoExtension.class)
class LobbyServiceTest {

    @Mock LobbyRepository lobbyRepository;
    @Mock GameRepository gameRepository;
    @Mock SseBroadcaster sseBroadcaster;
    @Mock SseEmitterRegistry sseEmitterRegistry;
    @Mock WordBank wordBank;

    LobbyService service;

    @BeforeEach
    void setUp() {
        service = new LobbyService(lobbyRepository, gameRepository, sseBroadcaster, sseEmitterRegistry, wordBank);
    }

    @Nested
    @DisplayName("createLobby")
    class CreateLobby {

        @Test
        @DisplayName("rejects players-per-team outside supported bounds")
        void rejectsInvalidPlayersPerTeam() {
            User user = new User(1, "host@test.com", "host");
            when(lobbyRepository.findByUserId(user.userId())).thenReturn(Optional.empty());

            assertThatThrownBy(() -> service.createLobby(new CreateLobbyRequest(1, 10), user))
                .isInstanceOf(IllegalArgumentException.class)
                .hasMessageContaining("between 2 and 5");

            assertThatThrownBy(() -> service.createLobby(new CreateLobbyRequest(6, 10), user))
                .isInstanceOf(IllegalArgumentException.class)
                .hasMessageContaining("between 2 and 5");

            verify(lobbyRepository, never()).create(anyInt(), eq("host"), eq("host@test.com"), anyInt(), anyInt());
        }

        @Test
        @DisplayName("rejects creating lobby when user is already in one")
        void rejectsWhenUserAlreadyInLobby() {
            User user = new User(1, "host@test.com", "host");
            Lobby existing = new Lobby("l-1", "ABC123", 1, "host", "host@test.com", 2, 10);
            when(lobbyRepository.findByUserId(user.userId())).thenReturn(Optional.of(existing));

            assertThatThrownBy(() -> service.createLobby(new CreateLobbyRequest(2, 10), user))
                .isInstanceOf(IllegalStateException.class)
                .hasMessageContaining("already in lobby");

            verify(lobbyRepository, never()).create(anyInt(), eq("host"), eq("host@test.com"), anyInt(), anyInt());
        }
    }

    @Nested
    @DisplayName("joinLobby")
    class JoinLobby {

        @Test
        @DisplayName("joins and broadcasts PLAYER_JOINED and LOBBY_SNAPSHOT")
        void joinsAndBroadcastsSnapshot() {
            Lobby lobby = new Lobby("l-1", "ABC123", 1, "host", "host@test.com", 2, 10);
            User joiner = new User(2, "u2@test.com", "u2");
            when(lobbyRepository.findByUserId(joiner.userId())).thenReturn(Optional.empty());
            when(lobbyRepository.findByCode("ABC123")).thenReturn(Optional.of(lobby));

            Lobby result = service.joinLobby("ABC123", joiner);

            assertThat(result.hasParticipant(joiner.userId())).isTrue();
            verify(sseBroadcaster).broadcast(
                eq("l-1"),
                eq(LobbyEventType.PLAYER_JOINED.name()),
                eq(Map.of("userId", 2, "username", "u2"))
            );
            verify(sseBroadcaster).broadcast(eq("l-1"), eq(LobbyEventType.LOBBY_SNAPSHOT.name()), eq(com.codenames.server.lobby.dto.LobbyStateResponse.from(lobby)));
        }

        @Test
        @DisplayName("rejects join when lobby is full")
        void rejectsWhenLobbyIsFull() {
            Lobby lobby = new Lobby("l-1", "ABC123", 1, "host", "host@test.com", 2, 10);
            lobby.tryAddParticipant(new LobbyParticipant(2, "u2", "u2@test.com"), 4);
            lobby.tryAddParticipant(new LobbyParticipant(3, "u3", "u3@test.com"), 4);
            lobby.tryAddParticipant(new LobbyParticipant(4, "u4", "u4@test.com"), 4);

            User joiner = new User(5, "u5@test.com", "u5");
            when(lobbyRepository.findByUserId(joiner.userId())).thenReturn(Optional.empty());
            when(lobbyRepository.findByCode("ABC123")).thenReturn(Optional.of(lobby));

            assertThatThrownBy(() -> service.joinLobby("ABC123", joiner))
                .isInstanceOf(IllegalStateException.class)
                .hasMessageContaining("Lobby is full");
        }
    }

    @Nested
    @DisplayName("leaveLobby")
    class LeaveLobby {

        @Test
        @DisplayName("removes lobby and channel when last participant leaves")
        void removesLobbyAndChannelWhenEmpty() {
            Lobby lobby = new Lobby("l-1", "ABC123", 1, "host", "host@test.com", 2, 10);
            User host = new User(1, "host@test.com", "host");
            when(lobbyRepository.findById("l-1")).thenReturn(Optional.of(lobby));

            service.leaveLobby("l-1", host);

            verify(lobbyRepository).remove("l-1");
            verify(sseEmitterRegistry).removeChannel("l-1");
            verify(sseBroadcaster, never()).broadcast(eq("l-1"), eq(LobbyEventType.PLAYER_LEFT.name()), eq(Map.of("userId", 1, "username", "host")));
        }

        @Test
        @DisplayName("transfers host when current host leaves non-empty lobby")
        void transfersHostWhenHostLeaves() {
            Lobby lobby = new Lobby("l-1", "ABC123", 1, "host", "host@test.com", 2, 10);
            lobby.tryAddParticipant(new LobbyParticipant(2, "u2", "u2@test.com"), 4);
            User host = new User(1, "host@test.com", "host");
            when(lobbyRepository.findById("l-1")).thenReturn(Optional.of(lobby));

            service.leaveLobby("l-1", host);

            assertThat(lobby.hostUserId()).isEqualTo(2);
            verify(sseBroadcaster).broadcast(eq("l-1"), eq(LobbyEventType.PLAYER_LEFT.name()), eq(Map.of("userId", 1, "username", "host")));
            verify(sseBroadcaster).broadcast(eq("l-1"), eq(LobbyEventType.LOBBY_SNAPSHOT.name()), eq(com.codenames.server.lobby.dto.LobbyStateResponse.from(lobby)));
        }
    }

    @Nested
    @DisplayName("startGame")
    class StartGame {

        @Test
        @DisplayName("rejects non-host starting the game")
        void rejectsNonHostStart() {
            Lobby lobby = fullLobby();
            User nonHost = new User(2, "u2@test.com", "u2");
            when(lobbyRepository.findById("l-1")).thenReturn(Optional.of(lobby));

            assertThatThrownBy(() -> service.startGame("l-1", nonHost))
                .isInstanceOf(IllegalStateException.class)
                .hasMessageContaining("Only the host");
        }

        @Test
        @DisplayName("rejects start when lobby is not full")
        void rejectsStartWhenNotFull() {
            Lobby lobby = new Lobby("l-1", "ABC123", 1, "host", "host@test.com", 2, 10);
            User host = new User(1, "host@test.com", "host");
            when(lobbyRepository.findById("l-1")).thenReturn(Optional.of(lobby));

            assertThatThrownBy(() -> service.startGame("l-1", host))
                .isInstanceOf(IllegalStateException.class)
                .hasMessageContaining("Exactly 4 players required");
        }

        @Test
        @DisplayName("creates game and inserts balanced participant assignments")
        void createsGameWithBalancedAssignments() {
            Lobby lobby = fullLobby();
            User host = new User(1, "host@test.com", "host");
            List<String> words = IntStream.range(0, 25).mapToObj(i -> "W" + i).toList();

            when(lobbyRepository.findById("l-1")).thenReturn(Optional.of(lobby));
            when(wordBank.selectRandom(25)).thenReturn(words);
            when(gameRepository.createGame(org.mockito.ArgumentMatchers.<List<Map<String, String>>>any(), eq(10))).thenReturn(77);

            int gameId = service.startGame("l-1", host);

            assertThat(gameId).isEqualTo(77);

            ArgumentCaptor<List<Map<String, Object>>> participantsCaptor = ArgumentCaptor.forClass(List.class);
            verify(gameRepository).insertParticipants(eq(77), participantsCaptor.capture());

            List<Map<String, Object>> assignments = participantsCaptor.getValue();
            assertThat(assignments).hasSize(4);
            long redCount = assignments.stream().filter(a -> "red".equals(a.get("team_name"))).count();
            long blueCount = assignments.stream().filter(a -> "blue".equals(a.get("team_name"))).count();
            long redSpymasterCount = assignments.stream().filter(a -> "red".equals(a.get("team_name")) && "spymaster".equals(a.get("role_name"))).count();
            long blueSpymasterCount = assignments.stream().filter(a -> "blue".equals(a.get("team_name")) && "spymaster".equals(a.get("role_name"))).count();

            assertThat(redCount).isEqualTo(2);
            assertThat(blueCount).isEqualTo(2);
            assertThat(redSpymasterCount).isEqualTo(1);
            assertThat(blueSpymasterCount).isEqualTo(1);

            verify(sseBroadcaster).broadcast(eq("l-1"), eq(LobbyEventType.GAME_STARTED.name()), eq(Map.of("gameId", 77)));
            verify(lobbyRepository).remove("l-1");
            verify(sseEmitterRegistry).removeChannel("l-1");
        }

        @Test
        @DisplayName("exposes flaw: less than 25 words causes index failure before explicit validation")
        void exposesWordBankSizeFlaw() {
            Lobby lobby = fullLobby();
            User host = new User(1, "host@test.com", "host");
            List<String> tooFewWords = IntStream.range(0, 24).mapToObj(i -> "W" + i).toList();

            when(lobbyRepository.findById("l-1")).thenReturn(Optional.of(lobby));
            when(wordBank.selectRandom(25)).thenReturn(tooFewWords);

            assertThatThrownBy(() -> service.startGame("l-1", host))
                .isInstanceOf(IndexOutOfBoundsException.class);

            verify(gameRepository, never()).createGame(org.mockito.ArgumentMatchers.<List<Map<String, String>>>any(), anyInt());
        }
    }

    @Nested
    @DisplayName("cleanupExpiredLobbies")
    class CleanupExpiredLobbies {

        @Test
        @DisplayName("removes SSE channels for all expired lobbies")
        void removesChannelsForExpiredLobbies() {
            when(lobbyRepository.removeExpired(org.mockito.ArgumentMatchers.any())).thenReturn(List.of("l-1", "l-2"));

            service.cleanupExpiredLobbies();

            verify(sseEmitterRegistry).removeChannel("l-1");
            verify(sseEmitterRegistry).removeChannel("l-2");
        }
    }

    private static Lobby fullLobby() {
        Lobby lobby = new Lobby("l-1", "ABC123", 1, "host", "host@test.com", 2, 10);
        List<LobbyParticipant> extra = new ArrayList<>();
        extra.add(new LobbyParticipant(2, "u2", "u2@test.com"));
        extra.add(new LobbyParticipant(3, "u3", "u3@test.com"));
        extra.add(new LobbyParticipant(4, "u4", "u4@test.com"));
        extra.forEach(p -> lobby.tryAddParticipant(p, 4));
        return lobby;
    }
}
