CREATE OR REPLACE FUNCTION get_game_remaining_counts(p_game_id INT)
RETURNS TABLE (
    red_remaining  INT,
    blue_remaining INT
)
LANGUAGE plpgsql
AS $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM game
        WHERE game_id   = p_game_id
        AND   is_deleted = FALSE
    ) THEN
        RAISE EXCEPTION 'Game % not found', p_game_id;
    END IF;

    RETURN QUERY
    SELECT g.red_remaining, g.blue_remaining
    FROM   game g
    WHERE  g.game_id = p_game_id;
END;
$$;
