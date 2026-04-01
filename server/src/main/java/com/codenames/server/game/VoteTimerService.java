package com.codenames.server.game;

import jakarta.annotation.PreDestroy;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Service;

import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.Executors;
import java.util.concurrent.ScheduledExecutorService;
import java.util.concurrent.ScheduledFuture;
import java.util.concurrent.TimeUnit;

@Service
public class VoteTimerService {

    private static final Logger logger = LoggerFactory.getLogger(VoteTimerService.class);

    private record TimerKey(int gameId, String team) {}

    private final ConcurrentHashMap<TimerKey, ScheduledFuture<?>> timers = new ConcurrentHashMap<>();
    private final ScheduledExecutorService scheduler = Executors.newScheduledThreadPool(4);

    private final int voteTimerSeconds;

    public VoteTimerService(@Value("${app.game.vote-timer-seconds:30}") int voteTimerSeconds) {
        this.voteTimerSeconds = voteTimerSeconds;
    }

    public void start(int gameId, String team, Runnable onExpiry) {
        TimerKey key = new TimerKey(gameId, team);
        cancelKey(key);

        ScheduledFuture<?> future = scheduler.schedule(
            () -> {
                timers.remove(key);
                logger.info("Vote timer expired for game {} team {}", gameId, team);
                onExpiry.run();
            },
            voteTimerSeconds,
            TimeUnit.SECONDS
        );
        timers.put(key, future);
        logger.debug("Vote timer started for game {} team {}", gameId, team);
    }

    public void cancel(int gameId, String team) {
        cancelKey(new TimerKey(gameId, team));
    }

    public void cancelAll(int gameId) {
        timers.keySet().stream()
            .filter(timerKey -> timerKey.gameId() == gameId)
            .forEach(this::cancelKey);
    }

    private void cancelKey(TimerKey key) {
        ScheduledFuture<?> existing = timers.remove(key);
        if (existing != null) {
            existing.cancel(false);
            logger.debug("Vote timer cancelled for game {} team {}", key.gameId(), key.team());
        }
    }

    @PreDestroy
    public void shutdown() {
        scheduler.shutdownNow();
    }
}
