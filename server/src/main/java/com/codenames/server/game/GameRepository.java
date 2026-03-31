package com.codenames.server.game;

import static com.codenames.server.shared.ExceptionUtils.rootMessage;

import com.codenames.server.game.dto.GameParticipantIdentityResponse;
import com.codenames.server.game.dto.GameWord;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.stereotype.Repository;
import tools.jackson.databind.ObjectMapper;

import java.sql.CallableStatement;
import java.sql.Types;
import java.util.List;
import java.util.Map;
import java.util.Optional;

@Repository
public class GameRepository {

    private final JdbcTemplate jdbcTemplate;
    private final ObjectMapper objectMapper;

    public GameRepository(JdbcTemplate jdbcTemplate, ObjectMapper objectMapper) {
        this.jdbcTemplate = jdbcTemplate;
        this.objectMapper = objectMapper;
    }

    public int createGame(List<Map<String, String>> words, int matchDurationMinutes) {
        try {
            String wordsJson = objectMapper.writeValueAsString(words);

            Integer gameId = jdbcTemplate.execute((java.sql.Connection conn) -> {
                try (CallableStatement cs = conn.prepareCall("CALL create_game(?::jsonb, ?, ?)")) {
                    cs.setString(1, wordsJson);
                    cs.setInt(2, matchDurationMinutes);
                    cs.registerOutParameter(3, Types.INTEGER);
                    cs.execute();
                    return cs.getInt(3);
                }
            });

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

    public Optional<Game> getGameById(int gameId) {
        try {
            record GameInfo(int id, String status) {}

            var results = jdbcTemplate.query(
                "SELECT g.game_id, gs.game_status FROM game g " +
                "INNER JOIN game_status gs ON gs.game_status_id = g.game_status_id " +
                "WHERE g.game_id = ?",
                (rs, rowNum) -> new GameInfo(rs.getInt("game_id"), rs.getString("game_status")),
                gameId
            );

            if (results.isEmpty()) {
                return Optional.empty();
            }

            GameInfo gameInfo = results.getFirst();

            List<GameWord> gameWords = jdbcTemplate.query(
                "SELECT * FROM get_game_words(?)",
                (rs, rowNum) -> new GameWord(
                    rs.getInt("game_word_id"),
                    rs.getString("word"),
                    rs.getString("word_type"),
                    rs.getBoolean("is_revealed")
                ),
                gameId
            );

            String dbStatus = gameInfo.status() != null ? gameInfo.status().toUpperCase() : "ACTIVE";
            if (!"ACTIVE".equals(dbStatus) && !"FINISHED".equals(dbStatus)) {
                dbStatus = "ACTIVE";
            }
            Game.GameStatus status = Game.GameStatus.valueOf(dbStatus);

            return Optional.of(new Game(gameInfo.id(), gameWords, status));
        } catch (Exception e) {
            throw new RuntimeException("Failed to fetch game: " + rootMessage(e), e);
        }
    }

    public GameParticipantIdentityResponse getParticipantIdentity(int gameId, int userId) {
        return jdbcTemplate.query(
                """
                SELECT gt.team_name, pr.role_name
                FROM game_participant gp
                JOIN game_team gt ON gt.game_team_id = gp.game_team_id
                JOIN participant_role pr ON pr.participant_role_id = gp.participant_role_id
                WHERE gp.game_id = ?
                  AND gp.user_id = ?
                  AND gp.is_deleted = FALSE
                LIMIT 1
                """,
                rs -> {
                    if (!rs.next()) {
                        return null;
                    }
                    return new GameParticipantIdentityResponse(
                            gameId,
                            rs.getString("team_name"),
                            rs.getString("role_name")
                    );
                },
                gameId,
                userId
        );
    }

}
