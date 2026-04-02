package com.codenames.server.game;

import com.codenames.server.game.dto.RoundResult;
import com.codenames.server.shared.sse.SseBroadcaster;
import com.codenames.server.shared.sse.SseEmitterRegistry;
import jakarta.annotation.PreDestroy;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.stereotype.Service;

import java.util.List;
import java.util.Map;
import java.util.concurrent.Executors;
import java.util.concurrent.ScheduledExecutorService;
import java.util.concurrent.TimeUnit;
import java.util.stream.Collectors;

@Service
public class VoteTallyService {

    private static final Logger logger = LoggerFactory.getLogger(VoteTallyService.class);
    private final ScheduledExecutorService scheduler = Executors.newSingleThreadScheduledExecutor();
    private final GameLockProvider gameLockProvider;

    @PreDestroy
    public void shutdown() {
        scheduler.shutdownNow();
    }

    private final GameRepository gameRepository;
    private final SseBroadcaster sseBroadcaster;
    private final SseEmitterRegistry sseEmitterRegistry;
    private final ClueTimerService clueTimerService;
    private final VoteTimerService voteTimerService;
    private final MatchTimerService matchTimerService;

    public VoteTallyService(
            GameRepository gameRepository,
            SseBroadcaster sseBroadcaster,
            SseEmitterRegistry sseEmitterRegistry,
            ClueTimerService clueTimerService,
            VoteTimerService voteTimerService,
            MatchTimerService matchTimerService,
            GameLockProvider gameLockProvider) {
        this.gameRepository = gameRepository;
        this.sseBroadcaster = sseBroadcaster;
        this.sseEmitterRegistry = sseEmitterRegistry;
        this.clueTimerService = clueTimerService;
        this.voteTimerService = voteTimerService;
        this.matchTimerService = matchTimerService;
        this.gameLockProvider = gameLockProvider;
    }

    public void tallyAndReveal(int gameId, String team, int roundId) {
        synchronized (gameLockProvider.get(gameId)) {
            logger.info("Tallying votes for game {} team {} round {}", gameId, team, roundId);

            GameRepository.GameMeta meta = gameRepository.getGameMeta(gameId);
            if (!"active".equals(meta.status())) {
                logger.info("Game {} already ended — skipping tally for team {} round {}", gameId, team, roundId);
                return;
            }

            if (gameRepository.findActiveRound(gameId, team).isEmpty()) {
                logger.info("Round {} for game {} team {} already resolved — skipping duplicate tally", roundId, gameId, team);
                return;
            }

            gameRepository.revealRoundWords(roundId);
            gameRepository.resolveGameRound(roundId);

            List<RoundResult> results = gameRepository.getRoundResults(roundId);

            List<Map<String, Object>> wordPayload = results.stream()
                .map(roundResult -> Map.<String, Object>of(
                    "word", roundResult.word(),
                    "wordType", roundResult.wordType(),
                    "outcome", roundResult.outcome(),
                    "voteCount", roundResult.voteCount()
                ))
                .collect(Collectors.toList());

            sseBroadcaster.broadcast(
                "game-" + gameId,
                GameEventType.WORDS_REVEALED.name(),
                Map.of("team", team, "words", wordPayload)
            );

            boolean hitAssassin = results.stream().anyMatch(RoundResult::isAssassin);
            if (hitAssassin) {
                String loser = team;
                String winner = "red".equals(loser) ? "blue" : "red";
                logger.info("Assassin hit by team {} in game {} — {} wins", loser, gameId, winner);
                endGameLocked(gameId, winner, "ASSASSIN");
                return;
            }

            int[] remaining = gameRepository.getGameRemainingCounts(gameId);
            int redRemaining  = remaining[0];
            int blueRemaining = remaining[1];

            if (redRemaining == 0) {
                logger.info("Red revealed all words in game {}", gameId);
                endGameLocked(gameId, "red", "ALL_REVEALED");
                return;
            }
            if (blueRemaining == 0) {
                logger.info("Blue revealed all words in game {}", gameId);
                endGameLocked(gameId, "blue", "ALL_REVEALED");
                return;
            }

            clueTimerService.start(gameId, team);
            sseBroadcaster.broadcast(
                "game-" + gameId,
                GameEventType.ROUND_STARTED.name(),
                Map.of("team", team)
            );
        }
    }

    public void endGame(int gameId, String winnerTeam, String reason) {
        synchronized (gameLockProvider.get(gameId)) {
            endGameLocked(gameId, winnerTeam, reason);
        }
    }

    private void endGameLocked(int gameId, String winnerTeam, String reason) {
        GameRepository.GameMeta meta = gameRepository.getGameMeta(gameId);
        if (!"active".equals(meta.status())) {
            logger.info("Game {} already ended — skipping duplicate endGame (winner={}, reason={})", gameId, winnerTeam, reason);
            return;
        }

        gameRepository.endGame(gameId, winnerTeam, reason);

        int[] remaining = gameRepository.getGameRemainingCounts(gameId);

        clueTimerService.cancelAll(gameId);
        voteTimerService.cancelAll(gameId);
        matchTimerService.cancel(gameId);

        sseBroadcaster.broadcast(
            "game-" + gameId,
            GameEventType.GAME_ENDED.name(),
            Map.of(
                "winner", winnerTeam != null ? winnerTeam : "draw",
                "reason", reason,
                "redRemaining", remaining[0],
                "blueRemaining", remaining[1]
            )
        );

        scheduler.schedule(() -> sseEmitterRegistry.removeChannel("game-" + gameId), 3, TimeUnit.SECONDS);
    }
}
