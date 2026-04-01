CREATE OR REPLACE FUNCTION get_active_round_for_team(p_game_id INT, p_team_name VARCHAR(50))
RETURNS TABLE (
    game_round_id INT,
    clue_word     VARCHAR(100),
    clue_number   INT
)
LANGUAGE plpgsql
AS $$
BEGIN
    RETURN QUERY
    SELECT gr.game_round_id, gr.clue_word, gr.clue_number
    FROM game_round gr
    JOIN game_team    gt ON gt.game_team_id    = gr.game_team_id
    JOIN round_status rs ON rs.round_status_id = gr.round_status_id
    WHERE gr.game_id      = p_game_id
    AND   gt.team_name    = p_team_name
    AND   rs.round_status = 'active'
    AND   gr.is_deleted   = FALSE
    LIMIT 1;
END;
$$;
