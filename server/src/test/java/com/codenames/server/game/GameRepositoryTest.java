package com.codenames.server.game;

import com.codenames.server.shared.exception.GameNotFoundException;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Nested;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.ArgumentMatchers;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.jdbc.core.RowMapper;
import tools.jackson.databind.ObjectMapper;

import java.time.Instant;
import java.util.List;
import java.util.Map;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.mockito.ArgumentMatchers.*;
import static org.mockito.Mockito.*;

@ExtendWith(MockitoExtension.class)
class GameRepositoryTest {

    @Mock JdbcTemplate jdbcTemplate;
    @Mock ObjectMapper objectMapper;

    GameRepository repository;

    @BeforeEach
    void setUp() {
        repository = new GameRepository(jdbcTemplate, objectMapper);
    }

    @Nested
    @DisplayName("getGameMeta")
    class GetGameMeta {

        @Test
        @DisplayName("returns GameMeta when game exists")
        void returnsGameMetaWhenGameExists() {
            Instant endsAt = Instant.parse("2026-04-01T12:00:00Z");
            GameRepository.GameMeta expected = new GameRepository.GameMeta(42, "active", endsAt, 8, 6);

            doReturn(List.of(expected))
                .when(jdbcTemplate).query(
                    anyString(),
                    ArgumentMatchers.<RowMapper<GameRepository.GameMeta>>any(),
                    eq(42));

            GameRepository.GameMeta result = repository.getGameMeta(42);

            assertThat(result.gameId()).isEqualTo(42);
            assertThat(result.status()).isEqualTo("active");
            assertThat(result.matchEndsAt()).isEqualTo(endsAt);
            assertThat(result.redRemaining()).isEqualTo(8);
            assertThat(result.blueRemaining()).isEqualTo(6);
        }

        @Test
        @DisplayName("throws GameNotFoundException when game does not exist")
        void throwsGameNotFoundWhenNoRows() {
            doReturn(List.of())
                .when(jdbcTemplate).query(
                    anyString(),
                    ArgumentMatchers.<RowMapper<GameRepository.GameMeta>>any(),
                    eq(99));

            assertThatThrownBy(() -> repository.getGameMeta(99))
                .isInstanceOf(GameNotFoundException.class)
                .hasMessageContaining("99");
        }
    }

    @Nested
    @DisplayName("getRoundVoteCounts")
    class GetRoundVoteCounts {

        @Test
        @DisplayName("queries get_round_vote_counts function, not round_result table")
        void queriesRoundVoteFunction() {
            doReturn(List.of())
                .when(jdbcTemplate).query(
                    anyString(),
                    ArgumentMatchers.<RowMapper<Map<String, Long>>>any(),
                    eq(7));

            Map<String, Long> result = repository.getRoundVoteCounts(7);

            assertThat(result).isEmpty();
            verify(jdbcTemplate).query(
                contains("get_round_vote_counts"),
                ArgumentMatchers.<RowMapper<Map<String, Long>>>any(),
                eq(7));
            verify(jdbcTemplate, never()).query(
                contains("round_result"),
                ArgumentMatchers.<RowMapper<Map<String, Long>>>any(),
                anyInt());
        }
    }
}
