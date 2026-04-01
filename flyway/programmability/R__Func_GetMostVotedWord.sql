CREATE OR REPLACE FUNCTION get_top_n_votes(p_game_round_id INT)
RETURNS TABLE (
    game_word_id INT,
    word         VARCHAR(100),
    word_type    VARCHAR(50),
    vote_count   BIGINT
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_clue_number INT;
BEGIN
    SELECT clue_number INTO v_clue_number
    FROM game_round
    WHERE game_round_id = p_game_round_id
    AND is_deleted = FALSE;

    IF v_clue_number IS NULL THEN
        RAISE EXCEPTION 'Round % not found', p_game_round_id;
    END IF;

    RETURN QUERY
    SELECT
        gw.game_word_id,
        gw.word,
        wt.type_name AS word_type,
        COUNT(*) AS vote_count
    FROM round_vote rv
    JOIN game_word gw ON gw.game_word_id = rv.game_word_id
    JOIN word_type wt ON wt.word_type_id = gw.word_type_id
    WHERE rv.game_round_id = p_game_round_id
    GROUP BY gw.game_word_id, gw.word, wt.type_name
    ORDER BY COUNT(*) DESC, gw.game_word_id ASC
    LIMIT v_clue_number;
END;
$$;