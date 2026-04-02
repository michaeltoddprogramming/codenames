package com.codenames.server.game;

import com.codenames.server.shared.sse.SseBroadcaster;
import com.codenames.server.shared.sse.SseEmitterRegistry;
import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Nested;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

import java.lang.reflect.Method;
import java.time.Instant;
import java.util.Map;

import static org.mockito.ArgumentMatchers.anyInt;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.Mockito.never;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;

@ExtendWith(MockitoExtension.class)
class MatchTimerServiceTest {

    @Mock GameRepository gameRepository;
    @Mock SseBroadcaster sseBroadcaster;
    @Mock SseEmitterRegistry sseEmitterRegistry;
    @Mock ClueTimerService clueTimerService;
    @Mock VoteTimerService voteTimerService;
    @Mock GameLockProvider gameLockProvider;

    MatchTimerService service;

    @BeforeEach
    void setUp() {
        service = new MatchTimerService(gameRepository, sseBroadcaster, sseEmitterRegistry, clueTimerService, voteTimerService, gameLockProvider);
    }

    @AfterEach
    void tearDown() {
        service.shutdown();
    }

    @Nested
    @DisplayName("onExpiry")
    class OnExpiry {

        @Test
        @DisplayName("declares red winner when red has fewer remaining words")
        void declaresRedWinner() throws Exception {
            int gameId = 1;
            Object lock = new Object();
            when(gameLockProvider.get(anyInt())).thenReturn(lock);
            when(gameRepository.getGameRemainingCounts(gameId)).thenReturn(new int[]{1, 3});
            when(gameRepository.getGameMeta(gameId)).thenReturn(
                new GameRepository.GameMeta(gameId, "active", java.time.Instant.now(), 1, 3));

            invokeOnExpiry(gameId);

            verify(gameRepository).endGame(gameId, "red", "TIMER_EXPIRED");
            verify(clueTimerService).cancelAll(gameId);
            verify(voteTimerService).cancelAll(gameId);
            verify(sseBroadcaster).broadcast(
                eq("game-1"),
                eq(GameEventType.GAME_ENDED.name()),
                eq(Map.of("winner", "red", "reason", "TIMER_EXPIRED", "redRemaining", 1, "blueRemaining", 3))
            );
        }

        @Test
        @DisplayName("declares draw when remaining counts are equal")
        void declaresDraw() throws Exception {
            int gameId = 2;
            Object lock = new Object();
            when(gameLockProvider.get(anyInt())).thenReturn(lock);
            when(gameRepository.getGameRemainingCounts(gameId)).thenReturn(new int[]{2, 2});
            when(gameRepository.getGameMeta(gameId)).thenReturn(
                new GameRepository.GameMeta(gameId, "active", java.time.Instant.now(), 2, 2));

            invokeOnExpiry(gameId);

            verify(gameRepository).endGame(gameId, null, "DRAW");
            verify(sseBroadcaster).broadcast(
                eq("game-2"),
                eq(GameEventType.GAME_ENDED.name()),
                eq(Map.of("winner", "draw", "reason", "DRAW", "redRemaining", 2, "blueRemaining", 2))
            );
        }

        @Test
        @DisplayName("swallows exceptions while ending game to keep scheduler thread alive")
        void swallowsExpiryExceptions() throws Exception {
            int gameId = 3;
            Object lock = new Object();
            when(gameLockProvider.get(anyInt())).thenReturn(lock);
            when(gameRepository.getGameMeta(gameId)).thenReturn(
                new GameRepository.GameMeta(gameId, "active", java.time.Instant.now(), 0, 0));
            when(gameRepository.getGameRemainingCounts(gameId)).thenThrow(new RuntimeException("db down"));

            invokeOnExpiry(gameId);

            verify(gameRepository, never()).endGame(eq(gameId), eq("red"), eq("TIMER_EXPIRED"));
            verify(gameRepository, never()).endGame(eq(gameId), eq("blue"), eq("TIMER_EXPIRED"));
            verify(sseBroadcaster, never()).broadcast(eq("game-3"), eq(GameEventType.GAME_ENDED.name()), org.mockito.ArgumentMatchers.any());
        }
    }

    @Nested
    @DisplayName("onTick")
    class OnTick {

        @Test
        @DisplayName("clamps negative remaining time to zero")
        void clampsNegativeRemainingTimeToZero() throws Exception {
            int gameId = 9;
            GameRepository.GameMeta meta = new GameRepository.GameMeta(
                gameId,
                "active",
                Instant.now().minusSeconds(10),
                5,
                5
            );
            when(gameRepository.getGameMeta(gameId)).thenReturn(meta);

            invokeOnTick(gameId);

            verify(sseBroadcaster).broadcast(
                eq("game-9"),
                eq(GameEventType.TIMER_TICK.name()),
                eq(Map.of("secondsRemaining", 0L))
            );
        }
    }

    private void invokeOnExpiry(int gameId) throws Exception {
        Method method = MatchTimerService.class.getDeclaredMethod("onExpiry", int.class);
        method.setAccessible(true);
        method.invoke(service, gameId);
    }

    private void invokeOnTick(int gameId) throws Exception {
        Method method = MatchTimerService.class.getDeclaredMethod("onTick", int.class, long.class);
        method.setAccessible(true);
        method.invoke(service, gameId, 60L);
    }
}
