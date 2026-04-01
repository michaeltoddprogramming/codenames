CREATE OR REPLACE PROCEDURE reveal_round_words(p_game_round_id INT)
LANGUAGE plpgsql
AS $$
DECLARE
    v_game_id      INT;
    v_team_name    VARCHAR(50);
    v_clue_number  INT;
    v_word_rec     RECORD;
    v_outcome_id   INT;
BEGIN
    SELECT gr.game_id, gt.team_name, gr.clue_number
    INTO   v_game_id, v_team_name, v_clue_number
    FROM   game_round gr
    JOIN   game_team  gt ON gt.game_team_id = gr.game_team_id
    WHERE  gr.game_round_id = p_game_round_id
    AND    gr.is_deleted    = FALSE;

    IF v_game_id IS NULL THEN
        RAISE EXCEPTION 'Round % not found', p_game_round_id;
    END IF;

    FOR v_word_rec IN
        SELECT gw.game_word_id, wt.type_name AS word_type, COUNT(*) AS vote_count
        FROM   round_vote rv
        JOIN   game_word  gw ON gw.game_word_id  = rv.game_word_id
        JOIN   word_type  wt ON wt.word_type_id  = gw.word_type_id
        WHERE  rv.game_round_id = p_game_round_id
        GROUP  BY gw.game_word_id, wt.type_name
        ORDER  BY COUNT(*) DESC, gw.game_word_id ASC
        LIMIT  v_clue_number
    LOOP
        UPDATE game_word
        SET    is_revealed = TRUE,
               updated_at  = NOW()
        WHERE  game_word_id = v_word_rec.game_word_id;

        IF v_word_rec.word_type = 'assassin' THEN
            SELECT reveal_outcome_id INTO v_outcome_id
            FROM   reveal_outcome WHERE outcome_name = 'assassin';

        ELSIF v_word_rec.word_type = v_team_name THEN
            SELECT reveal_outcome_id INTO v_outcome_id
            FROM   reveal_outcome WHERE outcome_name = 'correct';

        ELSIF v_word_rec.word_type = 'neutral' THEN
            SELECT reveal_outcome_id INTO v_outcome_id
            FROM   reveal_outcome WHERE outcome_name = 'neutral';

        ELSE
            SELECT reveal_outcome_id INTO v_outcome_id
            FROM   reveal_outcome WHERE outcome_name = 'opponent';
        END IF;

        INSERT INTO round_result (game_round_id, game_word_id, reveal_outcome_id, vote_count)
        VALUES (p_game_round_id, v_word_rec.game_word_id, v_outcome_id, v_word_rec.vote_count);

        IF v_word_rec.word_type = 'red' THEN
            UPDATE game
            SET    red_remaining = red_remaining - 1,
                   updated_at    = NOW()
            WHERE  game_id = v_game_id;

        ELSIF v_word_rec.word_type = 'blue' THEN
            UPDATE game
            SET    blue_remaining = blue_remaining - 1,
                   updated_at     = NOW()
            WHERE  game_id = v_game_id;
        END IF;
    END LOOP;
END;
$$;
