ALTER TABLE round_vote
    ADD CONSTRAINT uq_vote_per_participant_per_word_per_round
        UNIQUE (game_round_id, game_participant_id, game_word_id);
