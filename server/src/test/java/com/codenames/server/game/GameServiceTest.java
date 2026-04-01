package com.codenames.server.game;

import com.codenames.server.game.dto.ActiveRound;
import com.codenames.server.game.dto.GameParticipantInfo;
import com.codenames.server.game.dto.GameStateDetailResponse;
import com.codenames.server.game.dto.GameWord;
import com.codenames.server.game.dto.VoteRequest;
import com.codenames.server.shared.sse.SseBroadcaster;
import com.codenames.server.user.User;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Nested;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

import java.time.Instant;
import java.util.List;
import java.util.Map;
import java.util.Optional;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.mockito.ArgumentMatchers.*;
import static org.mockito.Mockito.*;

@ExtendWith(MockitoExtension.class)
class GameServiceTest {

    @Mock GameRepository gameRepository;
    @Mock SseBroadcaster sseBroadcaster;
    @Mock ClueTimerService clueTimerService;
    @Mock VoteTimerService voteTimerService;
    @Mock VoteTallyService voteTallyService;

    GameService service;
    User spymaster;
    User operative;

    @BeforeEach
    void setUp() {
        service = new GameService(gameRepository, sseBroadcaster, clueTimerService, voteTimerService, voteTallyService);
        spymaster = new User(1, "spy@test.com", "spy");
        operative  = new User(2, "op@test.com",  "op");
    }

    @Nested
    @DisplayName("buildGameState — word visibility")
    class BuildGameStateWordVisibility {

        private static final int GAME_ID = 10;
        private final Instant endsAt = Instant.parse("2026-04-01T14:00:00Z");
        private final GameRepository.GameMeta meta =
            new GameRepository.GameMeta(GAME_ID, "active", endsAt, 7, 8);

        private final List<GameWord> words = List.of(
            new GameWord(1, "TIGER",   "red",      false),
            new GameWord(2, "OCEAN",   "blue",     false),
            new GameWord(3, "HAMMER",  "neutral",  false),
            new GameWord(4, "VIPER",   "assassin", false),
            new GameWord(5, "REVEALED","red",      true)   // already revealed
        );

        @BeforeEach
        void stubCommon() {
            when(gameRepository.getGameMeta(GAME_ID)).thenReturn(meta);
            when(gameRepository.getGameById(GAME_ID))
                .thenReturn(Optional.of(new Game(GAME_ID, words, Game.GameStatus.ACTIVE)));
            when(gameRepository.findActiveRound(eq(GAME_ID), anyString()))
                .thenReturn(Optional.empty());
        }

        @Test
        @DisplayName("spymaster sees category for all words including unrevealed")
        void spymasterSeesAllCategories() {
            when(gameRepository.findParticipant(GAME_ID, spymaster.userId()))
                .thenReturn(new GameParticipantInfo(10, "red", "spymaster"));

            GameStateDetailResponse state = service.buildGameState(GAME_ID, spymaster);

            assertThat(state.words()).isNotEmpty().allSatisfy(w ->
                assertThat(w.category()).as("spymaster should see category for '%s'", w.word()).isNotNull()
            );
        }

        @Test
        @DisplayName("operative sees null category for unrevealed words")
        void operativeSeesNullCategoryForUnrevealed() {
            when(gameRepository.findParticipant(GAME_ID, operative.userId()))
                .thenReturn(new GameParticipantInfo(11, "red", "operative"));

            GameStateDetailResponse state = service.buildGameState(GAME_ID, operative);

            state.words().forEach(w -> {
                if (w.revealed()) {
                    assertThat(w.category()).as("revealed word '%s' should show category", w.word()).isNotNull();
                } else {
                    assertThat(w.category()).as("unrevealed word '%s' should hide category", w.word()).isNull();
                }
            });
        }

        @Test
        @DisplayName("buildGameState reads live vote counts from getRoundVoteCounts, not getRoundResults")
        void livVoteCountsComeFromRoundVoteNotRoundResult() {
            when(gameRepository.findParticipant(GAME_ID, operative.userId()))
                .thenReturn(new GameParticipantInfo(11, "red", "operative"));
            when(gameRepository.findActiveRound(GAME_ID, "red"))
                .thenReturn(Optional.of(new ActiveRound(5, "animal", 2)));
            when(gameRepository.getRoundVoteCounts(5))
                .thenReturn(Map.of("TIGER", 3L, "OCEAN", 1L));

            GameStateDetailResponse state = service.buildGameState(GAME_ID, operative);

            GameStateDetailResponse.ActiveRoundView redRound = state.activeRounds().get("red");
            assertThat(redRound).isNotNull();
            assertThat(redRound.votes()).containsEntry("TIGER", 3L).containsEntry("OCEAN", 1L);

            // Must NOT call getRoundResults for live tallies
            verify(gameRepository, never()).getRoundResults(anyInt());
        }
    }

    @Nested
    @DisplayName("submitVote — early tally")
    class SubmitVoteEarlyTally {

        @Test
        @DisplayName("triggers tally immediately when all vote slots are exhausted")
        void triggersEarlyTallyWhenAllVotesCast() {
            int gameId = 1;
            int roundId = 3;
            when(gameRepository.findParticipant(gameId, operative.userId()))
                .thenReturn(new GameParticipantInfo(11, "red", "operative"));
            when(gameRepository.findActiveRound(gameId, "red"))
                .thenReturn(Optional.of(new ActiveRound(roundId, "animal", 2)));
            // 2 operatives × clueNumber 2 = 4 total slots; simulate all 4 cast
            when(gameRepository.countVotesCast(roundId)).thenReturn(4);
            when(gameRepository.countOperatives(gameId, "red")).thenReturn(2);

            service.submitVote(gameId, operative, new VoteRequest("TIGER"));

            verify(voteTimerService).cancel(gameId, "red");
            verify(voteTallyService).tallyAndReveal(gameId, "red", roundId);
        }

        @Test
        @DisplayName("does not tally early when vote budget not yet exhausted")
        void doesNotTallyEarlyWhenBudgetNotExhausted() {
            int gameId = 1;
            int roundId = 3;
            when(gameRepository.findParticipant(gameId, operative.userId()))
                .thenReturn(new GameParticipantInfo(11, "red", "operative"));
            when(gameRepository.findActiveRound(gameId, "red"))
                .thenReturn(Optional.of(new ActiveRound(roundId, "animal", 2)));
            when(gameRepository.countVotesCast(roundId)).thenReturn(2);  // 2 of 4 used
            when(gameRepository.countOperatives(gameId, "red")).thenReturn(2);

            service.submitVote(gameId, operative, new VoteRequest("TIGER"));

            verify(voteTimerService, never()).cancel(anyInt(), anyString());
            verify(voteTallyService, never()).tallyAndReveal(anyInt(), anyString(), anyInt());
        }

        @Test
        @DisplayName("throws when user is not a participant")
        void throwsWhenNotParticipant() {
            when(gameRepository.findParticipant(1, operative.userId())).thenReturn(null);
            VoteRequest vote = new VoteRequest("TIGER");

            assertThatThrownBy(() -> service.submitVote(1, operative, vote))
                .isInstanceOf(IllegalArgumentException.class)
                .hasMessageContaining("not a participant");
        }

        @Test
        @DisplayName("throws when spymaster tries to vote")
        void throwsWhenSpymasterVotes() {
            when(gameRepository.findParticipant(1, spymaster.userId()))
                .thenReturn(new GameParticipantInfo(10, "red", "spymaster"));
            VoteRequest vote = new VoteRequest("TIGER");

            assertThatThrownBy(() -> service.submitVote(1, spymaster, vote))
                .isInstanceOf(IllegalArgumentException.class)
                .hasMessageContaining("Operatives");
        }
    }
}
