package com.codenames.server.game;

import static com.codenames.server.shared.ExceptionUtils.rootMessage;

import com.codenames.server.game.dto.ActiveRound;
import com.codenames.server.game.dto.GameParticipantIdentityResponse;
import com.codenames.server.game.dto.GameParticipantInfo;
import com.codenames.server.game.dto.GamePlayerResponse;
import com.codenames.server.game.dto.GameWord;
import com.codenames.server.game.dto.RoundResult;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.stereotype.Repository;
import tools.jackson.databind.ObjectMapper;

import java.sql.CallableStatement;
import java.sql.Types;
import java.time.Instant;
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

    public record GameMeta(int gameId, String status, Instant matchEndsAt, int redRemaining, int blueRemaining) {}

    public GameMeta getGameMeta(int gameId) {
        var results = jdbcTemplate.query(
            """
            SELECT g.game_id, gs.game_status, g.match_ends_at, g.red_remaining, g.blue_remaining
            FROM game g
            JOIN game_status gs ON gs.game_status_id = g.game_status_id
            WHERE g.game_id = ?
            """,
            (resultSet, rowNum) -> {
                java.sql.Timestamp matchEndsAt = resultSet.getTimestamp("match_ends_at");
                return new GameMeta(
                    resultSet.getInt("game_id"),
                    resultSet.getString("game_status"),
                    matchEndsAt != null ? matchEndsAt.toInstant() : null,
                    resultSet.getInt("red_remaining"),
                    resultSet.getInt("blue_remaining")
                );
            },
            gameId
        );
        if (results.isEmpty()) {
            throw new com.codenames.server.shared.exception.GameNotFoundException("Game not found: " + gameId);
        }
        return results.getFirst();
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
                resultSet -> {
                    if (!resultSet.next()) {
                        return null;
                    }
                    return new GameParticipantIdentityResponse(
                            gameId,
                            resultSet.getString("team_name"),
                            resultSet.getString("role_name")
                    );
                },
                gameId,
                userId
        );
    }

    public GameParticipantInfo findParticipant(int gameId, int userId) {
        return jdbcTemplate.query(
            """
            SELECT gp.game_participant_id, gt.team_name, pr.role_name
            FROM game_participant gp
            JOIN game_team gt ON gt.game_team_id = gp.game_team_id
            JOIN participant_role pr ON pr.participant_role_id = gp.participant_role_id
            WHERE gp.game_id = ?
              AND gp.user_id = ?
              AND gp.is_deleted = FALSE
            LIMIT 1
            """,
            resultSet -> {
                if (!resultSet.next()) return null;
                return new GameParticipantInfo(
                    resultSet.getInt("game_participant_id"),
                    resultSet.getString("team_name"),
                    resultSet.getString("role_name")
                );
            },
            gameId,
            userId
        );
    }

    public List<GamePlayerResponse> getGamePlayers(int gameId) {
        return jdbcTemplate.query(
            "SELECT * FROM get_game_players(?)",
            (rs, rowNum) -> new GamePlayerResponse(
                rs.getString("username"),
                rs.getString("team_name"),
                rs.getString("role_name")
            ),
            gameId
        );
    }

    public Optional<Integer> findActiveGameForUser(int userId) {
        var results = jdbcTemplate.query(
            """
            SELECT gp.game_id
            FROM game_participant gp
            JOIN game g ON g.game_id = gp.game_id
            JOIN game_status gs ON gs.game_status_id = g.game_status_id
            WHERE gp.user_id = ?
              AND gs.game_status = 'active'
              AND gp.is_deleted = FALSE
              AND g.is_deleted = FALSE
            LIMIT 1
            """,
            (resultSet, rowNum) -> resultSet.getInt("game_id"),
            userId
        );
        return results.isEmpty() ? Optional.empty() : Optional.of(results.getFirst());
    }

    public int insertGameRound(int gameId, String teamName, String clueWord, int clueNumber) {
        try {
            jdbcTemplate.update(
                "CALL insert_game_round(?, ?, ?, ?)",
                gameId, teamName, clueWord, clueNumber
            );
        } catch (Exception e) {
            String msg = rootMessage(e);
            if (msg.contains("already has an active round")) {
                throw new IllegalStateException(msg, e);
            }
            throw new RuntimeException("Failed to insert game round: " + msg, e);
        }

        var results = jdbcTemplate.query(
            "SELECT * FROM get_active_round_for_team(?, ?)",
            (resultSet, rowNum) -> resultSet.getInt("game_round_id"),
            gameId,
            teamName
        );

        if (results.isEmpty()) {
            throw new IllegalStateException("Round was inserted but could not be retrieved for game " + gameId + " team " + teamName);
        }
        return results.getFirst();
    }

    public Optional<ActiveRound> findActiveRound(int gameId, String teamName) {
        var results = jdbcTemplate.query(
            "SELECT * FROM get_active_round_for_team(?, ?)",
            (resultSet, rowNum) -> new ActiveRound(
                resultSet.getInt("game_round_id"),
                resultSet.getString("clue_word"),
                resultSet.getInt("clue_number")
            ),
            gameId,
            teamName
        );
        return results.isEmpty() ? Optional.empty() : Optional.of(results.getFirst());
    }

    public void resolveGameRound(int roundId) {
        try {
            jdbcTemplate.update("CALL resolve_game_round(?)", roundId);
        } catch (Exception e) {
            throw new RuntimeException("Failed to resolve round: " + rootMessage(e), e);
        }
    }

    public void insertVote(int roundId, String word, int userId) {
        try {
            jdbcTemplate.update("CALL insert_round_vote(?, ?, ?)", roundId, word, userId);
        } catch (Exception e) {
            String msg = rootMessage(e);
            if (msg.contains("has already voted for")
                    || msg.contains("has already used all")
                    || msg.contains("is not available in game")
                    || msg.contains("does not belong to the team")
                    || msg.contains("Spymasters cannot cast votes")
                    || msg.contains("duplicate key value")) {
                throw new IllegalArgumentException(msg, e);
            }
            throw new RuntimeException("Failed to insert vote: " + msg, e);
        }
    }

    public int countVotesCast(int roundId) {
        Integer count = jdbcTemplate.queryForObject(
            "SELECT COUNT(*) FROM round_vote WHERE game_round_id = ?",
            Integer.class,
            roundId
        );
        return count != null ? count : 0;
    }

    public int countOperatives(int gameId, String teamName) {
        Integer count = jdbcTemplate.queryForObject(
            """
            SELECT COUNT(*)
            FROM game_participant gp
            JOIN game_team gt ON gt.game_team_id = gp.game_team_id
            JOIN participant_role pr ON pr.participant_role_id = gp.participant_role_id
            WHERE gp.game_id = ?
              AND gt.team_name = ?
              AND pr.role_name = 'operative'
              AND gp.is_deleted = FALSE
            """,
            Integer.class,
            gameId,
            teamName
        );
        return count != null ? count : 0;
    }

    public void revealRoundWords(int roundId) {
        try {
            jdbcTemplate.update("CALL reveal_round_words(?)", roundId);
        } catch (Exception e) {
            throw new RuntimeException("Failed to reveal round words: " + rootMessage(e), e);
        }
    }

    public Map<String, Long> getRoundVoteCounts(int roundId) {
        Map<String, Long> counts = new java.util.LinkedHashMap<>();
        jdbcTemplate.query(
            "SELECT * FROM get_round_vote_counts(?)",
            (resultSet, rowNum) -> {
                counts.put(resultSet.getString("word"), resultSet.getLong("vote_count"));
                return null;
            },
            roundId
        );
        return counts;
    }

    public List<RoundResult> getRoundResults(int roundId) {
        return jdbcTemplate.query(
            """
            SELECT gw.game_word_id, gw.word, wt.type_name AS word_type,
                   ro.outcome_name, rr.vote_count
            FROM round_result rr
            JOIN game_word gw ON gw.game_word_id = rr.game_word_id
            JOIN word_type wt ON wt.word_type_id = gw.word_type_id
            JOIN reveal_outcome ro ON ro.reveal_outcome_id = rr.reveal_outcome_id
            WHERE rr.game_round_id = ?
            """,
            (resultSet, rowNum) -> new RoundResult(
                resultSet.getInt("game_word_id"),
                resultSet.getString("word"),
                resultSet.getString("word_type"),
                resultSet.getString("outcome_name"),
                resultSet.getInt("vote_count")
            ),
            roundId
        );
    }

    public int[] getGameRemainingCounts(int gameId) {
        return jdbcTemplate.query(
            "SELECT * FROM get_game_remaining_counts(?)",
            resultSet -> {
                if (!resultSet.next()) throw new IllegalStateException("Game " + gameId + " not found");
                return new int[]{ resultSet.getInt("red_remaining"), resultSet.getInt("blue_remaining") };
            },
            gameId
        );
    }

    public void endGame(int gameId, String winnerTeamName, String endReason) {
        try {
            jdbcTemplate.update("CALL end_game(?, ?, ?)", gameId, winnerTeamName, endReason);
        } catch (Exception e) {
            throw new RuntimeException("Failed to end game: " + rootMessage(e), e);
        }
    }
}
