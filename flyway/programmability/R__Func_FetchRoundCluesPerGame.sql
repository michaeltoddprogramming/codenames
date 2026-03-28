CREATE OR REPLACE FUNCTION get_game_rounds(p_game_id INT)
RETURNS TABLE (
    game_round_id INT,
    team_name     VARCHAR(50),
    round_status  VARCHAR(50),
    clue_word     VARCHAR(100),
    clue_number   INT,
    created_at    TIMESTAMPTZ
)
LANGUAGE plpgsql
AS $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM game WHERE game_id = p_game_id) THEN
        RAISE EXCEPTION 'Game % not found', p_game_id;
    END IF;

    RETURN QUERY
    SELECT
        gr.game_round_id,
        gt.team_name,
        rs.round_status,
        gr.clue_word,
        gr.clue_number,
        gr.created_at
    FROM game_round gr
    JOIN game_team    gt ON gt.game_team_id    = gr.game_team_id
    JOIN round_status rs ON rs.round_status_id = gr.round_status_id
    WHERE gr.game_id    = p_game_id
    AND   gr.is_deleted = FALSE
    ORDER BY gr.created_at;
END;
$$;