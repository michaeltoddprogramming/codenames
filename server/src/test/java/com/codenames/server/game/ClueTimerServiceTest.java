package com.codenames.server.game;

import com.codenames.server.game.dto.ActiveRound;
import com.codenames.server.shared.sse.SseBroadcaster;
import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

import java.lang.reflect.Field;
import java.lang.reflect.Method;
import java.util.Map;
import java.util.Optional;
import java.util.concurrent.ConcurrentHashMap;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;

@ExtendWith(MockitoExtension.class)
class ClueTimerServiceTest {

    @Mock SseBroadcaster sseBroadcaster;
    @Mock GameRepository gameRepository;

    ClueTimerService service;

    @BeforeEach
    void setUp() {
        service = new ClueTimerService(sseBroadcaster, gameRepository, 90);
    }

    @AfterEach
    void tearDown() {
        service.shutdown();
    }

    @Test
    @DisplayName("onExpiry broadcasts TURN_SKIPPED and immediately re-arms timer when no active round")
    void onExpiryBroadcastsAndRearmsTimer() throws Exception {
        int gameId = 10;
        when(gameRepository.getGameMeta(gameId)).thenReturn(
            new GameRepository.GameMeta(gameId, "active", java.time.Instant.now().plusSeconds(60), 8, 8));
        when(gameRepository.findActiveRound(gameId, "red")).thenReturn(Optional.empty());

        invokeOnExpiry(gameId, "red");

        verify(sseBroadcaster).broadcast(
            eq("game-10"),
            eq(GameEventType.TURN_SKIPPED.name()),
            eq(Map.of("team", "red", "reason", "SPYMASTER_TIMEOUT"))
        );

        assertThat(timerMap().size()).isEqualTo(1);
    }

    @Test
    @DisplayName("cancelAll only removes timers for the provided game id")
    void cancelAllOnlyRemovesTargetGameTimers() throws Exception {
        service.start(1, "red");
        service.start(1, "blue");
        service.start(2, "red");

        assertThat(timerMap().size()).isEqualTo(3);

        service.cancelAll(1);

        assertThat(timerMap().size()).isEqualTo(1);

        service.cancel(2, "red");
        assertThat(timerMap().size()).isEqualTo(0);
    }

    @Test
    @DisplayName("starting same game/team twice keeps a single active timer entry")
    void startReplacesExistingTimerForSameTeam() throws Exception {
        service.start(99, "blue");
        assertThat(timerMap().size()).isEqualTo(1);

        service.start(99, "blue");

        assertThat(timerMap().size()).isEqualTo(1);
    }

    private void invokeOnExpiry(int gameId, String team) throws Exception {
        Method method = ClueTimerService.class.getDeclaredMethod("onExpiry", int.class, String.class);
        method.setAccessible(true);
        method.invoke(service, gameId, team);
    }

    @SuppressWarnings("unchecked")
    private ConcurrentHashMap<Object, Object> timerMap() throws Exception {
        Field timersField = ClueTimerService.class.getDeclaredField("timers");
        timersField.setAccessible(true);
        return (ConcurrentHashMap<Object, Object>) timersField.get(service);
    }
}
