CREATE TABLE "user" (
    user_id        SERIAL PRIMARY KEY,
    username       VARCHAR(100) NOT NULL,
    email          VARCHAR(255) NOT NULL,
    oauth_provider VARCHAR(50)  NOT NULL,
    oauth_subject  VARCHAR(255) NOT NULL,
    is_deleted     BOOLEAN      NOT NULL DEFAULT FALSE,
    created_at     TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at     TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
);

CREATE TABLE game (
    game_id        SERIAL PRIMARY KEY,
    game_status_id INT         NOT NULL,
    is_deleted     BOOLEAN     NOT NULL DEFAULT FALSE,
    created_at     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
);

CREATE TABLE game_participant (
    game_participant_id SERIAL PRIMARY KEY,
    user_id             INT         NOT NULL,
    game_id             INT         NOT NULL,
    game_team_id        INT         NOT NULL,
    participant_role_id INT         NOT NULL,
    is_deleted          BOOLEAN     NOT NULL DEFAULT FALSE,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
);

CREATE TABLE game_word (
    game_word_id SERIAL PRIMARY KEY,
    game_id      INT          NOT NULL,
    word_type_id INT          NOT NULL,
    word         VARCHAR(100) NOT NULL,
    is_revealed  BOOLEAN      NOT NULL DEFAULT FALSE,
    is_deleted   BOOLEAN      NOT NULL DEFAULT FALSE,
    created_at   TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at   TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
);

CREATE TABLE game_round (
    game_round_id   SERIAL PRIMARY KEY,
    game_id         INT          NOT NULL,
    game_team_id    INT          NOT NULL,
    round_status_id INT          NOT NULL,
    clue_word       VARCHAR(100) NOT NULL,
    clue_number     INT          NOT NULL,
    expires_at      TIMESTAMPTZ  NOT NULL,
    is_deleted      BOOLEAN      NOT NULL DEFAULT FALSE,
    created_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
);

CREATE TABLE round_vote (
    round_vote_id       SERIAL PRIMARY KEY,
    game_round_id       INT         NOT NULL,
    game_word_id        INT         NOT NULL,
    game_participant_id INT         NOT NULL,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE round_result (
    round_result_id   SERIAL PRIMARY KEY,
    game_round_id     INT         NOT NULL,
    game_word_id      INT         NOT NULL,
    reveal_outcome_id INT         NOT NULL,
    vote_count        INT         NOT NULL,
    created_at        TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

ALTER TABLE game
ADD CONSTRAINT fk_game_game_status
FOREIGN KEY (game_status_id) REFERENCES game_status (game_status_id);

ALTER TABLE game_participant
ADD CONSTRAINT fk_game_participant_user
FOREIGN KEY (user_id) REFERENCES "user" (user_id);

ALTER TABLE game_participant
ADD CONSTRAINT fk_game_participant_game
FOREIGN KEY (game_id) REFERENCES game (game_id);

ALTER TABLE game_participant
ADD CONSTRAINT fk_game_participant_game_team
FOREIGN KEY (game_team_id) REFERENCES game_team (game_team_id);

ALTER TABLE game_participant
ADD CONSTRAINT fk_game_participant_participant_role
FOREIGN KEY (participant_role_id) REFERENCES participant_role (participant_role_id);

ALTER TABLE game_word
ADD CONSTRAINT fk_game_word_game
FOREIGN KEY (game_id) REFERENCES game (game_id);

ALTER TABLE game_word
ADD CONSTRAINT fk_game_word_word_type
FOREIGN KEY (word_type_id) REFERENCES word_type (word_type_id);

ALTER TABLE game_round
ADD CONSTRAINT fk_game_round_game
FOREIGN KEY (game_id) REFERENCES game (game_id);

ALTER TABLE game_round
ADD CONSTRAINT fk_game_round_game_team
FOREIGN KEY (game_team_id) REFERENCES game_team (game_team_id);

ALTER TABLE game_round
ADD CONSTRAINT fk_game_round_round_status
FOREIGN KEY (round_status_id) REFERENCES round_status (round_status_id);

ALTER TABLE round_vote
ADD CONSTRAINT fk_round_vote_game_round
FOREIGN KEY (game_round_id) REFERENCES game_round (game_round_id);

ALTER TABLE round_vote
ADD CONSTRAINT fk_round_vote_game_word
FOREIGN KEY (game_word_id) REFERENCES game_word (game_word_id);

ALTER TABLE round_vote
ADD CONSTRAINT fk_round_vote_game_participant
FOREIGN KEY (game_participant_id) REFERENCES game_participant (game_participant_id);

ALTER TABLE round_result
ADD CONSTRAINT fk_round_result_game_round
FOREIGN KEY (game_round_id) REFERENCES game_round (game_round_id);

ALTER TABLE round_result
ADD CONSTRAINT fk_round_result_game_word
FOREIGN KEY (game_word_id) REFERENCES game_word (game_word_id);

ALTER TABLE round_result
ADD CONSTRAINT fk_round_result_reveal_outcome
FOREIGN KEY (reveal_outcome_id) REFERENCES reveal_outcome (reveal_outcome_id);

ALTER TABLE "user"
ADD CONSTRAINT unique_username UNIQUE (username);

ALTER TABLE "user"
ADD CONSTRAINT unique_email UNIQUE (email);

ALTER TABLE "user"
ADD CONSTRAINT unique_oauth_combination UNIQUE (oauth_provider, oauth_subject);

ALTER TABLE game_participant
ADD CONSTRAINT unique_game_participant_details UNIQUE (user_id, game_id, game_team_id, participant_role_id);

ALTER TABLE game_word
ADD CONSTRAINT unique_game_word_details UNIQUE (game_id, word_type_id, word);