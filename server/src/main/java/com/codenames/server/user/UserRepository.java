package com.codenames.server.user;

import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.stereotype.Repository;

@Repository
public class UserRepository {

    private final JdbcTemplate jdbcTemplate;

    public UserRepository(JdbcTemplate jdbcTemplate) {
        this.jdbcTemplate = jdbcTemplate;
    }

    public User findOrCreate(String oauthSubject, String email, String username) {
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
}
