package com.codenames.server.user;

import static com.codenames.server.shared.ExceptionUtils.rootMessage;

import org.springframework.dao.DataIntegrityViolationException;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.stereotype.Repository;

@Repository
public class UserRepository {

    private static final int MAX_USERNAME_ATTEMPTS = 20;

    private final JdbcTemplate jdbcTemplate;

    public UserRepository(JdbcTemplate jdbcTemplate) {
        this.jdbcTemplate = jdbcTemplate;
    }

    public User findOrCreate(String oauthSubject, String email, String username) {
        String baseUsername = normalizeUsername(username);

        for (int attempt = 0; attempt < MAX_USERNAME_ATTEMPTS; attempt++) {
            String candidate = attempt == 0 ? baseUsername : baseUsername + "_" + (attempt + 1);
            try {
                return upsertByGoogleSubject(oauthSubject, email, candidate);
            } catch (DataIntegrityViolationException exception) {
                String message = rootMessage(exception);
                if (message != null && message.contains("unique_username")) {
                    continue;
                }
                throw exception;
            }
        }

        throw new IllegalStateException("Unable to allocate a unique username for this account");
    }

    private User upsertByGoogleSubject(String oauthSubject, String email, String username) {
        return jdbcTemplate.queryForObject(
                """
                INSERT INTO "user" (username, email, oauth_provider, oauth_subject)
                VALUES (?, ?, 'google', ?)
                ON CONFLICT (oauth_provider, oauth_subject) DO UPDATE SET updated_at = NOW()
                RETURNING user_id, username, email
                """,
                (resultSet, rowNumber) -> new User(
                        resultSet.getInt("user_id"),
                        resultSet.getString("email"),
                        resultSet.getString("username")
                ),
                username, email, oauthSubject
        );
    }

    private static String normalizeUsername(String username) {
        if (username == null) {
            return "player";
        }

        String trimmed = username.trim();
        return trimmed.isEmpty() ? "player" : trimmed;
    }

}
