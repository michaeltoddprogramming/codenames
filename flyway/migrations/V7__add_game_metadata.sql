ALTER TABLE game
    ADD COLUMN match_duration_minutes  INT         NOT NULL DEFAULT 5,
    ADD COLUMN match_started_at        TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    ADD COLUMN match_ends_at           TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    ADD COLUMN red_remaining   INT NOT NULL DEFAULT 8,
    ADD COLUMN blue_remaining  INT NOT NULL DEFAULT 8,
    ADD COLUMN winner_team_id  INT REFERENCES game_team(game_team_id),
    ADD COLUMN end_reason      VARCHAR(50),
    ADD COLUMN ended_at        TIMESTAMPTZ;
