package com.codenames.server.game;

import com.codenames.server.shared.sse.SseBroadcaster;
import jakarta.annotation.PreDestroy;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Service;

import java.util.Map;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.Executors;
import java.util.concurrent.ScheduledExecutorService;
import java.util.concurrent.ScheduledFuture;
import java.util.concurrent.TimeUnit;

@Service
public class ClueTimerService {

    private static final Logger logger = LoggerFactory.getLogger(ClueTimerService.class);

    private record TimerKey(int gameId, String team) {}

    private final ConcurrentHashMap<TimerKey, ScheduledFuture<?>> timers = new ConcurrentHashMap<>();
    private final ScheduledExecutorService scheduler = Executors.newScheduledThreadPool(4);

    private final SseBroadcaster sseBroadcaster;
    private final int clueTimerSeconds;

    public ClueTimerService(
            SseBroadcaster sseBroadcaster,
            @Value("${app.game.clue-timer-seconds:90}") int clueTimerSeconds) {
        this.sseBroadcaster = sseBroadcaster;
        this.clueTimerSeconds = clueTimerSeconds;
    }

    public void start(int gameId, String team) {
        TimerKey key = new TimerKey(gameId, team);
        cancelKey(key);

        ScheduledFuture<?> future = scheduler.schedule(
            () -> onExpiry(gameId, team),
            clueTimerSeconds,
            TimeUnit.SECONDS
        );
        timers.put(key, future);
        logger.debug("Clue timer started for game {} team {}", gameId, team);
    }

    public void cancel(int gameId, String team) {
        cancelKey(new TimerKey(gameId, team));
    }

    public void cancelAll(int gameId) {
        timers.keySet().stream()
            .filter(timerKey  -> timerKey.gameId() == gameId)
            .forEach(timerKey -> cancelKey(timerKey));
    }

    private void onExpiry(int gameId, String team) {
        timers.remove(new TimerKey(gameId, team));
        logger.info("Clue timer expired for game {} team {} — broadcasting TURN_SKIPPED", gameId, team);

        sseBroadcaster.broadcast(
            "game-" + gameId,
            GameEventType.TURN_SKIPPED.name(),
            Map.of("team", team, "reason", "SPYMASTER_TIMEOUT")
        );

        start(gameId, team);
    }

    private void cancelKey(TimerKey key) {
        ScheduledFuture<?> existing = timers.remove(key);
        if (existing != null) {
            existing.cancel(false);
        }
    }

    @PreDestroy
    public void shutdown() {
        scheduler.shutdownNow();
    }
}
