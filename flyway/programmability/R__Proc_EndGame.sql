CREATE OR REPLACE PROCEDURE end_game(
    p_game_id          INT,
    p_winner_team_name VARCHAR(50),
    p_end_reason       VARCHAR(50)
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_finished_status_id INT;
    v_winner_team_id     INT;
BEGIN
    SELECT game_status_id INTO v_finished_status_id
    FROM   game_status
    WHERE  game_status = 'finished';

    IF p_winner_team_name IS NOT NULL THEN
        SELECT game_team_id INTO v_winner_team_id
        FROM   game_team
        WHERE  team_name = p_winner_team_name;

        IF v_winner_team_id IS NULL THEN
            RAISE EXCEPTION 'Team "%" not found', p_winner_team_name;
        END IF;
    END IF;

    UPDATE game
    SET    game_status_id = v_finished_status_id,
           winner_team_id = v_winner_team_id,
           end_reason     = p_end_reason,
           ended_at       = NOW(),
           updated_at     = NOW()
    WHERE  game_id    = p_game_id
    AND    is_deleted = FALSE;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'Game % not found', p_game_id;
    END IF;
END;
$$;
