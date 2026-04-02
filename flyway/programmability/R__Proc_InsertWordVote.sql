CREATE OR REPLACE PROCEDURE insert_round_vote(
    p_game_round_id INT,
    p_word          VARCHAR(100),
    p_user_id       INT
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_game_id             INT;
    v_game_word_id        INT;
    v_game_participant_id INT;
    v_clue_number         INT;
    v_votes_cast          INT;
BEGIN
    SELECT gr.game_id, gr.clue_number, gp.game_participant_id
    INTO v_game_id, v_clue_number, v_game_participant_id
    FROM game_round gr
    JOIN round_status rs ON rs.round_status_id = gr.round_status_id
    LEFT JOIN game_participant gp ON gp.game_id = gr.game_id
                                 AND gp.game_team_id = gr.game_team_id
                                 AND gp.user_id = p_user_id
                                 AND gp.is_deleted = FALSE
    WHERE gr.game_round_id = p_game_round_id
    AND rs.round_status = 'active'
    AND gr.is_deleted = FALSE;

    IF v_game_id IS NULL THEN
        RAISE EXCEPTION 'Active round % not found', p_game_round_id;
    END IF;

    IF v_game_participant_id IS NULL THEN
        IF EXISTS (
            SELECT 1 FROM game_participant gp
            WHERE gp.user_id = p_user_id
            AND gp.game_id = v_game_id
            AND gp.is_deleted = FALSE
        ) THEN
            RAISE EXCEPTION 'User id % does not belong to the team playing round %', p_user_id, p_game_round_id;
        END IF;

        RAISE EXCEPTION 'User id % is not a participant in game %', p_user_id, v_game_id;
    END IF;

    SELECT gw.game_word_id INTO v_game_word_id
    FROM game_word gw
    WHERE gw.word = p_word
    AND gw.game_id = v_game_id
    AND gw.is_revealed = FALSE
    AND gw.is_deleted = FALSE;

    IF v_game_word_id IS NULL THEN
        RAISE EXCEPTION 'Word "%" is not available in game %', p_word, v_game_id;
    END IF;

    IF EXISTS (
        SELECT 1 FROM game_participant gp
        JOIN participant_role pr ON pr.participant_role_id = gp.participant_role_id
        WHERE gp.game_participant_id = v_game_participant_id
        AND pr.role_name = 'spymaster'
    ) THEN
        RAISE EXCEPTION 'Spymasters cannot cast votes';
    END IF;


    IF EXISTS (
        SELECT 1 FROM round_vote
        WHERE game_round_id = p_game_round_id
        AND game_participant_id = v_game_participant_id
        AND   game_word_id = v_game_word_id
    ) THEN
        RAISE EXCEPTION 'User id % has already voted for "%" in round %', p_user_id, p_word, p_game_round_id;
    END IF;

    SELECT COUNT(*) INTO v_votes_cast
    FROM round_vote
    WHERE game_round_id = p_game_round_id
    AND   game_participant_id = v_game_participant_id;

    IF v_votes_cast >= v_clue_number THEN
        RAISE EXCEPTION 'User id % has already used all % votes for round %', p_user_id, v_clue_number, p_game_round_id;
    END IF;

    INSERT INTO round_vote (game_round_id, game_word_id, game_participant_id)
    VALUES (p_game_round_id, v_game_word_id, v_game_participant_id);
END;
$$;