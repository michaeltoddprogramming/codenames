package com.codenames.server.game;

import com.codenames.server.shared.sse.SseBroadcaster;
import com.codenames.server.shared.sse.SseEmitterRegistry;
import jakarta.annotation.PreDestroy;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.stereotype.Service;

import java.util.ArrayList;
import java.util.List;
import java.util.Map;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.Executors;
import java.util.concurrent.ScheduledExecutorService;
import java.util.concurrent.ScheduledFuture;
import java.util.concurrent.TimeUnit;

@Service
public class MatchTimerService {

    private static final Logger logger = LoggerFactory.getLogger(MatchTimerService.class);
    private static final int TICK_INTERVAL_SECONDS = 10;

    private final ConcurrentHashMap<Integer, List<ScheduledFuture<?>>> gameTimers = new ConcurrentHashMap<>();
    private final ScheduledExecutorService scheduler = Executors.newScheduledThreadPool(4);

    private final GameRepository gameRepository;
    private final SseBroadcaster sseBroadcaster;
    private final SseEmitterRegistry sseEmitterRegistry;
    private final ClueTimerService clueTimerService;
    private final VoteTimerService voteTimerService;

    public MatchTimerService(
            GameRepository gameRepository,
            SseBroadcaster sseBroadcaster,
            SseEmitterRegistry sseEmitterRegistry,
            ClueTimerService clueTimerService,
            VoteTimerService voteTimerService) {
        this.gameRepository = gameRepository;
        this.sseBroadcaster = sseBroadcaster;
        this.sseEmitterRegistry = sseEmitterRegistry;
        this.clueTimerService = clueTimerService;
        this.voteTimerService = voteTimerService;
    }

    public void start(int gameId, int matchDurationMinutes) {
        cancel(gameId);

        long totalSeconds = (long) matchDurationMinutes * 60;

        ScheduledFuture<?> tickFuture = scheduler.scheduleAtFixedRate(
            () -> onTick(gameId, totalSeconds),
            TICK_INTERVAL_SECONDS,
            TICK_INTERVAL_SECONDS,
            TimeUnit.SECONDS
        );

        ScheduledFuture<?> expiryFuture = scheduler.schedule(
            () -> onExpiry(gameId),
            totalSeconds,
            TimeUnit.SECONDS
        );

        List<ScheduledFuture<?>> futures = new ArrayList<>();
        futures.add(tickFuture);
        futures.add(expiryFuture);
        gameTimers.put(gameId, futures);

        logger.info("Match timer started for game {} — {} minutes", gameId, matchDurationMinutes);
    }

    public void cancel(int gameId) {
        List<ScheduledFuture<?>> futures = gameTimers.remove(gameId);
        if (futures != null) {
            futures.forEach(scheduledFuture -> scheduledFuture.cancel(false));
            logger.debug("Match timer cancelled for game {}", gameId);
        }
    }

    private void onTick(int gameId, long totalSeconds) {
        try {
            GameRepository.GameMeta meta = gameRepository.getGameMeta(gameId);
            long secondsRemaining = meta.matchEndsAt().getEpochSecond() - System.currentTimeMillis() / 1000;
            if (secondsRemaining < 0) secondsRemaining = 0;

            sseBroadcaster.broadcast(
                "game-" + gameId,
                GameEventType.TIMER_TICK.name(),
                Map.of("secondsRemaining", secondsRemaining)
            );
        } catch (Exception e) {
            logger.error("Error broadcasting TIMER_TICK for game {}: {}", gameId, e.getMessage());
        }
    }

    private void onExpiry(int gameId) {
        gameTimers.remove(gameId);
        logger.info("Match timer expired for game {}", gameId);

        try {
            int[] remaining = gameRepository.getGameRemainingCounts(gameId);
            int redRemaining  = remaining[0];
            int blueRemaining = remaining[1];

            String winner;
            String reason;

            if (redRemaining < blueRemaining) {
                winner = "red";
                reason = "TIMER_EXPIRED";
            } else if (blueRemaining < redRemaining) {
                winner = "blue";
                reason = "TIMER_EXPIRED";
            } else {
                winner = null;
                reason = "DRAW";
            }

            gameRepository.endGame(gameId, winner, reason);

            clueTimerService.cancelAll(gameId);
            voteTimerService.cancelAll(gameId);

            sseBroadcaster.broadcast(
                "game-" + gameId,
                GameEventType.GAME_ENDED.name(),
                Map.of(
                    "winner", winner != null ? winner : "draw",
                    "reason", reason,
                    "redRemaining", redRemaining,
                    "blueRemaining", blueRemaining
                )
            );

            scheduler.schedule(() -> sseEmitterRegistry.removeChannel("game-" + gameId), 3, TimeUnit.SECONDS);

        } catch (Exception e) {
            logger.error("Error ending game {} on timer expiry: {}", gameId, e.getMessage(), e);
        }
    }

    @PreDestroy
    public void shutdown() {
        scheduler.shutdownNow();
    }
}
