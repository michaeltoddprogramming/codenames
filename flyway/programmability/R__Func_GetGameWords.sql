CREATE OR REPLACE FUNCTION get_game_words(p_game_id INT)
RETURNS TABLE (
    game_word_id INT,
    word         VARCHAR(100),
    word_type    VARCHAR(50),
    is_revealed  BOOLEAN
)
LANGUAGE plpgsql
AS $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM game WHERE game_id = p_game_id) THEN
        RAISE EXCEPTION 'Game % not found', p_game_id;
    END IF;

    RETURN QUERY
    SELECT
        gw.game_word_id,
        gw.word,
        wt.type_name,
        gw.is_revealed
    FROM game_word gw
    JOIN word_type wt ON wt.word_type_id = gw.word_type_id
    WHERE gw.game_id = p_game_id
    AND   gw.is_deleted = FALSE
    ORDER BY gw.game_word_id;
END;
$$;