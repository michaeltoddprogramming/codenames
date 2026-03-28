CREATE OR REPLACE PROCEDURE insert_round_vote(
    p_game_round_id INT,
    p_word          VARCHAR(100),
    p_username      VARCHAR(100)
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_game_id             INT;
    v_game_word_id        INT;
    v_game_participant_id INT;
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM game_round gr
        JOIN round_status rs ON rs.round_status_id = gr.round_status_id
        WHERE gr.game_round_id = p_game_round_id
        AND rs.round_status = 'active'
        AND gr.is_deleted = FALSE
    ) THEN
        RAISE EXCEPTION 'Active round % not found', p_game_round_id;
    END IF;

    SELECT game_id INTO v_game_id
    FROM game_round
    WHERE game_round_id = p_game_round_id;
 
    SELECT gw.game_word_id INTO v_game_word_id
    FROM game_word gw
    WHERE gw.word = p_word
    AND gw.game_id = v_game_id
    AND gw.is_deleted = FALSE;

    IF v_game_word_id IS NULL THEN
        RAISE EXCEPTION 'Word "%" does not belong to game %', p_word, v_game_id;
    END IF;

    SELECT gp.game_participant_id INTO v_game_participant_id
    FROM game_participant gp
    JOIN "user" u ON u.user_id = gp.user_id
    WHERE u.username    = p_username
    AND   gp.game_id    = v_game_id
    AND   gp.is_deleted = FALSE;

    IF v_game_participant_id IS NULL THEN
        RAISE EXCEPTION 'User "%" is not a participant in game %', p_username, v_game_id;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM game_participant gp
        JOIN game_round gr ON gr.game_team_id = gp.game_team_id
        WHERE gp.game_participant_id = v_game_participant_id
        AND gr.game_round_id = p_game_round_id
        AND gp.is_deleted = FALSE
    ) THEN
        RAISE EXCEPTION 'User "%" does not belong to the team playing round %', p_username, p_game_round_id;
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
        SELECT 1 FROM game_word
        WHERE game_word_id = v_game_word_id
        AND is_revealed = TRUE
    ) THEN
        RAISE EXCEPTION 'Word "%" has already been revealed', p_word;
    END IF;

    IF EXISTS (
        SELECT 1 FROM round_vote
        WHERE game_round_id = p_game_round_id
        AND game_participant_id = v_game_participant_id
    ) THEN
        RAISE EXCEPTION 'User "%" has already voted in round %', p_username, p_game_round_id;
    END IF;

    INSERT INTO round_vote (game_round_id, game_word_id, game_participant_id)
    VALUES (p_game_round_id, v_game_word_id, v_game_participant_id);
END;
$$;