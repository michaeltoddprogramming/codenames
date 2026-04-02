package com.codenames.server.game;

import com.codenames.server.shared.sse.SseBroadcaster;
import jakarta.annotation.PreDestroy;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Service;

import java.util.Map;
import java.util.Optional;
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
    private final ConcurrentHashMap<TimerKey, Long> deadlines = new ConcurrentHashMap<>(); // epoch ms
    private final ScheduledExecutorService scheduler = Executors.newScheduledThreadPool(4);

    private final SseBroadcaster sseBroadcaster;
    private final GameRepository gameRepository;
    private final int clueTimerSeconds;

    public ClueTimerService(
            SseBroadcaster sseBroadcaster,
            GameRepository gameRepository,
            @Value("${app.game.clue-timer-seconds:60}") int clueTimerSeconds) {
        this.sseBroadcaster = sseBroadcaster;
        this.gameRepository = gameRepository;
        this.clueTimerSeconds = clueTimerSeconds;
    }

    public void start(int gameId, String team) {
        TimerKey key = new TimerKey(gameId, team);
        cancelKey(key);

        long endsAtEpochMs = System.currentTimeMillis() + (long) clueTimerSeconds * 1000;
        deadlines.put(key, endsAtEpochMs);

        ScheduledFuture<?> future = scheduler.schedule(
            () -> onExpiry(gameId, team),
            clueTimerSeconds,
            TimeUnit.SECONDS
        );
        timers.put(key, future);

        sseBroadcaster.broadcast(
            "game-" + gameId,
            GameEventType.CLUE_TIMER_STARTED.name(),
            Map.of("team", team, "endsAtEpochMs", endsAtEpochMs, "durationSeconds", clueTimerSeconds)
        );
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

        try {
            GameRepository.GameMeta meta = gameRepository.getGameMeta(gameId);
            if (!"active".equals(meta.status())) {
                logger.info("Clue timer expired for game {} team {} but game already ended — skipping", gameId, team);
                deadlines.remove(new TimerKey(gameId, team));
                return;
            }
        } catch (Exception e) {
            logger.warn("Clue timer expired for game {} team {} but could not check game status — skipping", gameId, team, e);
            deadlines.remove(new TimerKey(gameId, team));
            return;
        }

        try {
            if (gameRepository.findActiveRound(gameId, team).isPresent()) {
                logger.info("Clue timer expired for game {} team {} but active round already exists — spymaster submitted just before timeout, skipping", gameId, team);
                deadlines.remove(new TimerKey(gameId, team));
                return;
            }
        } catch (Exception e) {
            logger.warn("Clue timer expired for game {} team {} but could not check active round — skipping re-arm", gameId, team, e);
            deadlines.remove(new TimerKey(gameId, team));
            return;
        }

        logger.info("Clue timer expired for game {} team {} — broadcasting TURN_SKIPPED", gameId, team);

        sseBroadcaster.broadcast(
            "game-" + gameId,
            GameEventType.TURN_SKIPPED.name(),
            Map.of("team", team, "reason", "SPYMASTER_TIMEOUT")
        );

        start(gameId, team);
    }

    public Optional<Long> getDeadlineEpochMs(int gameId, String team) {
        return Optional.ofNullable(deadlines.get(new TimerKey(gameId, team)));
    }

    public int getDurationSeconds() {
        return clueTimerSeconds;
    }

    private void cancelKey(TimerKey key) {
        ScheduledFuture<?> existing = timers.remove(key);
        deadlines.remove(key);
        if (existing != null) {
            existing.cancel(false);
        }
    }

    @PreDestroy
    public void shutdown() {
        scheduler.shutdownNow();
    }
}
