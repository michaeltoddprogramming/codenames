CREATE OR REPLACE PROCEDURE insert_game_round(
    p_game_id    INT,
    p_team_name  VARCHAR(50),
    p_clue_word  VARCHAR(100),
    p_clue_number INT
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_game_team_id      INT;
    v_round_status_id   INT;
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM game g
        JOIN game_status gs ON gs.game_status_id = g.game_status_id
        WHERE g.game_id      = p_game_id
        AND   gs.game_status = 'active'
        AND   g.is_deleted   = FALSE
    ) THEN
        RAISE EXCEPTION 'Active game % not found', p_game_id;
    END IF;

    SELECT game_team_id INTO v_game_team_id
    FROM game_team
    WHERE team_name = p_team_name;

    IF v_game_team_id IS NULL THEN
        RAISE EXCEPTION 'Team "%" not found', p_team_name;
    END IF;

    IF EXISTS (
        SELECT 1
        FROM game_round gr
        JOIN round_status rs ON rs.round_status_id = gr.round_status_id
        WHERE gr.game_id      = p_game_id
        AND   gr.game_team_id = v_game_team_id
        AND   rs.round_status = 'active'
        AND   gr.is_deleted   = FALSE
    ) THEN
        RAISE EXCEPTION 'Team "%" already has an active round in game %', p_team_name, p_game_id;
    END IF;

    SELECT round_status_id INTO v_round_status_id
    FROM round_status
    WHERE round_status = 'active';

    INSERT INTO game_round (game_id, game_team_id, round_status_id, clue_word, clue_number)
    VALUES (p_game_id, v_game_team_id, v_round_status_id, p_clue_word, p_clue_number);
END;
$$;