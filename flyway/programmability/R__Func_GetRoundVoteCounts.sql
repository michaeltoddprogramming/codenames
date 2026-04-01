CREATE OR REPLACE FUNCTION get_round_vote_counts(p_game_round_id INT)
RETURNS TABLE (
    game_word_id INT,
    word         VARCHAR(100),
    vote_count   BIGINT
)
LANGUAGE plpgsql
AS $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM game_round
        WHERE game_round_id = p_game_round_id
        AND   is_deleted    = FALSE
    ) THEN
        RAISE EXCEPTION 'Round % not found', p_game_round_id;
    END IF;

    RETURN QUERY
    SELECT
        gw.game_word_id,
        gw.word,
        COUNT(*) AS vote_count
    FROM round_vote rv
    JOIN game_word gw ON gw.game_word_id = rv.game_word_id
    WHERE rv.game_round_id = p_game_round_id
    GROUP BY gw.game_word_id, gw.word
    ORDER BY COUNT(*) DESC, gw.game_word_id ASC;
END;
$$;
