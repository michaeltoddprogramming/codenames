CREATE OR REPLACE PROCEDURE resolve_game_round(p_game_round_id INT)
LANGUAGE plpgsql
AS $$
DECLARE
    v_resolved_status_id INT;
BEGIN
    SELECT round_status_id INTO v_resolved_status_id
    FROM   round_status
    WHERE  round_status = 'resolved';

    UPDATE game_round
    SET    round_status_id = v_resolved_status_id,
           updated_at      = NOW()
    WHERE  game_round_id = p_game_round_id
    AND    is_deleted     = FALSE;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'Round % not found', p_game_round_id;
    END IF;
END;
$$;
