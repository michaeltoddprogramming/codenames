CREATE OR REPLACE FUNCTION get_round_top_three_votes(p_game_round_id INT)
RETURNS TABLE (
    game_word_id INT,
    word         VARCHAR(100),
    word_type    VARCHAR(50),
    vote_count   BIGINT
)
LANGUAGE plpgsql
AS $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM game_round
        WHERE game_round_id = p_game_round_id
        AND is_deleted = FALSE
    ) THEN
        RAISE EXCEPTION 'Round % not found', p_game_round_id;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM round_vote
        WHERE game_round_id = p_game_round_id
    ) THEN
        RAISE EXCEPTION 'No votes have been cast for round %', p_game_round_id;
    END IF;

    RETURN QUERY
    WITH vote_counts AS (
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
    )
    SELECT
        vc.game_word_id,
        vc.word,
        vc.word_type,
        vc.vote_count
    FROM vote_counts vc
    WHERE vc.rnk <= 3;
END;
$$;