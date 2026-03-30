package com.codenames.server.game;

import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.stereotype.Repository;
import tools.jackson.databind.ObjectMapper;

import java.util.List;
import java.util.Map;

@Repository
public class GameRepository {

    private final JdbcTemplate jdbcTemplate;
    private final ObjectMapper objectMapper;

    public GameRepository(JdbcTemplate jdbcTemplate, ObjectMapper objectMapper) {
        this.jdbcTemplate = jdbcTemplate;
        this.objectMapper = objectMapper;
    }

    public int createGame(List<Map<String, String>> words) {
        try {
            String wordsJson = objectMapper.writeValueAsString(words);

            Integer gameId = jdbcTemplate.queryForObject(
                    "CALL create_game(CAST(? AS jsonb), NULL)",
                    Integer.class,
                    wordsJson
            );

            if (gameId == null) {
                throw new IllegalStateException("create_game returned no game id");
            }

            return gameId;
        } catch (Exception e) {
            throw new RuntimeException("Failed to create game: " + rootMessage(e), e);
        }
    }

    public void insertParticipants(int gameId, List<Map<String, Object>> participants) {
        try {
            String participantsJson = objectMapper.writeValueAsString(participants);

            jdbcTemplate.update(
                    "CALL insert_game_participants(?, CAST(? AS jsonb))",
                    gameId,
                    participantsJson
            );
        } catch (Exception e) {
            throw new RuntimeException("Failed to insert participants: " + rootMessage(e), e);
        }
    }

    private static String rootMessage(Throwable throwable) {
        Throwable current = throwable;
        while (current.getCause() != null && current.getCause() != current) {
            current = current.getCause();
        }
        return current.getMessage() != null ? current.getMessage() : throwable.getMessage();
    }
}
