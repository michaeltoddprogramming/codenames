CREATE OR REPLACE FUNCTION get_game_players(p_game_id INT)
RETURNS TABLE (
    game_participant_id INT,
    username            VARCHAR(100),
    team_name           VARCHAR(50),
    role_name           VARCHAR(50)
)
LANGUAGE plpgsql
AS $$
BEGIN
    IF NOT EXISTS (
      SELECT 1 FROM game g
      WHERE g.game_id = p_game_id
      AND g.is_deleted = FALSE
    ) THEN
        RAISE EXCEPTION 'Game % not found', p_game_id;
    END IF;

    RETURN QUERY
    SELECT
        gp.game_participant_id,
        u.username,
        gt.team_name,
        pr.role_name
    FROM game_participant gp
    JOIN "user" u  ON u.user_id = gp.user_id
    JOIN game_team gt ON gt.game_team_id = gp.game_team_id
    JOIN participant_role pr ON pr.participant_role_id = gp.participant_role_id
    WHERE gp.game_id = p_game_id
    AND gp.is_deleted = FALSE
    AND u.is_deleted = FALSE
    ORDER BY gt.team_name, pr.role_name, u.username;
END;
$$;