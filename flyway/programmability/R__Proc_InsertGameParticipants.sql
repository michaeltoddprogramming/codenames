CREATE OR REPLACE PROCEDURE insert_game_participants(
    p_game_id      INT,
    p_participants JSONB
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_invalid_users TEXT;
    v_invalid_teams TEXT;
    v_invalid_roles TEXT;
    v_insert_count  INT;
BEGIN
    IF NOT EXISTS (
      SELECT 1 FROM game g
      WHERE g.game_id = p_game_id
      AND g.is_deleted = FALSE
    ) THEN
        RAISE EXCEPTION 'Game % not found', p_game_id;
    END IF;

    IF jsonb_typeof(p_participants) <> 'array' THEN
        RAISE EXCEPTION 'p_participants must be a JSON array';
    END IF;

    IF jsonb_array_length(p_participants) = 0 THEN
        RAISE EXCEPTION 'Participant list cannot be empty';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM jsonb_array_elements(p_participants) AS p
        WHERE p->>'user_id'   IS NULL
        OR    p->>'team_name' IS NULL
        OR    p->>'role_name' IS NULL
    ) THEN
        RAISE EXCEPTION 'Each entry must have "user_id", "team_name", and "role_name" fields';
    END IF;

    SELECT string_agg(DISTINCT p->>'user_id', ', ')
    INTO v_invalid_users
    FROM jsonb_array_elements(p_participants) AS p
    WHERE NOT EXISTS (
        SELECT 1 FROM "user" u 
        WHERE u.user_id = (p->>'user_id')::INT
        AND u.is_deleted = FALSE
    );

    IF v_invalid_users IS NOT NULL THEN
        RAISE EXCEPTION 'Invalid or deleted user_id(s): %', v_invalid_users;
    END IF;

    SELECT string_agg(DISTINCT p->>'team_name', ', ')
    INTO v_invalid_teams
    FROM jsonb_array_elements(p_participants) AS p
    WHERE NOT EXISTS (
        SELECT 1 FROM game_team gt WHERE gt.team_name = p->>'team_name'
    );

    IF v_invalid_teams IS NOT NULL THEN
        RAISE EXCEPTION 'Invalid team_name(s): %', v_invalid_teams;
    END IF;

    SELECT string_agg(DISTINCT p->>'role_name', ', ')
    INTO v_invalid_roles
    FROM jsonb_array_elements(p_participants) AS p
    WHERE NOT EXISTS (
        SELECT 1 FROM participant_role pr WHERE pr.role_name = p->>'role_name'
    );

    IF v_invalid_roles IS NOT NULL THEN
        RAISE EXCEPTION 'Invalid role_name(s): %', v_invalid_roles;
    END IF;

    SELECT string_agg(DISTINCT p->>'user_id', ', ')
    INTO v_invalid_users
    FROM jsonb_array_elements(p_participants) AS p
    WHERE EXISTS (
        SELECT 1 FROM game_participant gp
        WHERE gp.user_id = (p->>'user_id')::INT
        AND gp.game_id = p_game_id
        AND gp.is_deleted = FALSE
    );

    IF v_invalid_users IS NOT NULL THEN
        RAISE EXCEPTION 'User(s) % are already participants in game %', v_invalid_users, p_game_id;
    END IF;

    INSERT INTO game_participant (game_id, user_id, game_team_id, participant_role_id)
    SELECT
        p_game_id,
        (p->>'user_id')::INT,
        gt.game_team_id,
        pr.participant_role_id
    FROM jsonb_array_elements(p_participants) AS p
    JOIN game_team gt ON gt.team_name  = p->>'team_name'
    JOIN participant_role pr ON pr.role_name  = p->>'role_name'
    ON CONFLICT ON CONSTRAINT unique_game_participant_details DO NOTHING;

    GET DIAGNOSTICS v_insert_count = ROW_COUNT;

    IF v_insert_count <> jsonb_array_length(p_participants) THEN
        RAISE EXCEPTION 'Some participants were not inserted — possible duplicates. Expected %, got %', jsonb_array_length(p_participants), v_insert_count;
    END IF;
END;
$$;