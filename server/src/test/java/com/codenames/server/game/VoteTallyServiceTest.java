package com.codenames.server.game;

import com.codenames.server.game.dto.RoundResult;
import com.codenames.server.shared.sse.SseBroadcaster;
import com.codenames.server.shared.sse.SseEmitterRegistry;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Nested;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

import java.util.List;
import java.util.Map;

import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.Mockito.never;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;

@ExtendWith(MockitoExtension.class)
class VoteTallyServiceTest {

    @Mock GameRepository gameRepository;
    @Mock SseBroadcaster sseBroadcaster;
    @Mock SseEmitterRegistry sseEmitterRegistry;
    @Mock ClueTimerService clueTimerService;
    @Mock VoteTimerService voteTimerService;
    @Mock MatchTimerService matchTimerService;

    VoteTallyService service;

    @BeforeEach
    void setUp() {
        service = new VoteTallyService(
            gameRepository,
            sseBroadcaster,
            sseEmitterRegistry,
            clueTimerService,
            voteTimerService,
            matchTimerService
        );
    }

    @Nested
    @DisplayName("tallyAndReveal")
    class TallyAndReveal {

        @Test
        @DisplayName("ends game when assassin is revealed")
        void endsGameWhenAssassinIsRevealed() {
            int gameId = 10;
            int roundId = 21;
            when(gameRepository.getRoundResults(roundId)).thenReturn(List.of(
                new RoundResult(5, "VIPER", "assassin", "assassin", 2)
            ));
            when(gameRepository.getGameRemainingCounts(gameId)).thenReturn(new int[]{7, 6});

            service.tallyAndReveal(gameId, "red", roundId);

            verify(gameRepository).revealRoundWords(roundId);
            verify(gameRepository).resolveGameRound(roundId);
            verify(gameRepository).endGame(gameId, "blue", "ASSASSIN");
            verify(clueTimerService).cancelAll(gameId);
            verify(voteTimerService).cancelAll(gameId);
            verify(matchTimerService).cancel(gameId);
            verify(sseBroadcaster).broadcast(
                eq("game-" + gameId),
                eq(GameEventType.GAME_ENDED.name()),
                eq(Map.of("winner", "blue", "reason", "ASSASSIN", "redRemaining", 7, "blueRemaining", 6))
            );
        }

        @Test
        @DisplayName("ends game when red has no remaining words")
        void endsGameWhenRedWordsAreFinished() {
            int gameId = 10;
            int roundId = 22;
            when(gameRepository.getRoundResults(roundId)).thenReturn(List.of(
                new RoundResult(1, "TIGER", "red", "correct", 3)
            ));
            when(gameRepository.getGameRemainingCounts(gameId)).thenReturn(new int[]{0, 2});

            service.tallyAndReveal(gameId, "red", roundId);

            verify(gameRepository).endGame(gameId, "red", "ALL_REVEALED");
            verify(clueTimerService).cancelAll(gameId);
            verify(voteTimerService).cancelAll(gameId);
            verify(matchTimerService).cancel(gameId);
        }

        @Test
        @DisplayName("starts next clue phase when game should continue")
        void startsNextCluePhaseWhenGameContinues() {
            int gameId = 10;
            int roundId = 23;
            when(gameRepository.getRoundResults(roundId)).thenReturn(List.of(
                new RoundResult(7, "OCEAN", "blue", "opponent", 2)
            ));
            when(gameRepository.getGameRemainingCounts(gameId)).thenReturn(new int[]{5, 4});

            service.tallyAndReveal(gameId, "red", roundId);

            verify(gameRepository, never()).endGame(gameId, "red", "ALL_REVEALED");
            verify(gameRepository, never()).endGame(gameId, "blue", "ALL_REVEALED");
            verify(clueTimerService).start(gameId, "red");
            verify(sseBroadcaster).broadcast(
                eq("game-" + gameId),
                eq(GameEventType.ROUND_STARTED.name()),
                eq(Map.of("team", "red"))
            );
        }
    }

    @Nested
    @DisplayName("endGame")
    class EndGame {

        @Test
        @DisplayName("publishes GAME_ENDED payload with draw winner label when winner team is null")
        void publishesGameEndedWithDrawWinner() {
            int gameId = 44;
            when(gameRepository.getGameRemainingCounts(gameId)).thenReturn(new int[]{3, 3});

            service.endGame(gameId, null, "DRAW");

            verify(gameRepository).endGame(gameId, null, "DRAW");
            verify(sseBroadcaster).broadcast(
                eq("game-" + gameId),
                eq(GameEventType.GAME_ENDED.name()),
                eq(Map.of("winner", "draw", "reason", "DRAW", "redRemaining", 3, "blueRemaining", 3))
            );
        }
    }
}
