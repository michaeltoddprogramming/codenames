CREATE TABLE participant_role (
    participant_role_id SERIAL PRIMARY KEY,
    role_name           VARCHAR(50) NOT NULL,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE word_type (
    word_type_id SERIAL PRIMARY KEY,
    type_name    VARCHAR(50) NOT NULL,
    created_at   TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE game_status (
    game_status_id SERIAL PRIMARY KEY,
    game_status   VARCHAR(50) NOT NULL,
    created_at     TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE round_status (
    round_status_id SERIAL PRIMARY KEY,
    round_status     VARCHAR(50) NOT NULL,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE reveal_outcome (
    reveal_outcome_id SERIAL PRIMARY KEY,
    outcome_name      VARCHAR(50) NOT NULL,
    created_at        TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE game_team (
    game_team_id SERIAL PRIMARY KEY,
    team_name    VARCHAR(50) NOT NULL,
    created_at   TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

ALTER TABLE participant_role
ADD CONSTRAINT unique_role_name UNIQUE (role_name);

ALTER TABLE word_type
ADD CONSTRAINT unique_type_name UNIQUE (type_name);

ALTER TABLE game_status
ADD CONSTRAINT unique_game_status UNIQUE (game_status);

ALTER TABLE round_status
ADD CONSTRAINT unique_round_status UNIQUE (round_status);

ALTER TABLE reveal_outcome
ADD CONSTRAINT unique_outcome_name UNIQUE (outcome_name);

ALTER TABLE game_team
ADD CONSTRAINT unique_team_name UNIQUE (team_name);