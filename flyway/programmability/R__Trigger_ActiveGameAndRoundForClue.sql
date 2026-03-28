CREATE OR REPLACE FUNCTION enforce_active_game_and_round_for_clue()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    IF TG_OP = 'UPDATE'
       AND OLD.clue_word IS NOT DISTINCT FROM NEW.clue_word
       AND OLD.clue_number IS NOT DISTINCT FROM NEW.clue_number THEN
        RETURN NEW;
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM game g
        INNER JOIN game_status gs ON gs.game_status_id = g.game_status_id
        INNER JOIN round_status rs ON rs.round_status_id = NEW.round_status_id
        WHERE g.game_id = NEW.game_id
          AND gs.game_status = 'active'
          AND rs.round_status = 'active'
          AND NOT g.is_deleted
    ) THEN
        RAISE EXCEPTION 'Clues are only allowed when the game and round are active.'
            USING ERRCODE = 'check_violation';
    END IF;
    RETURN NEW;
END;
$$;

CREATE OR REPLACE TRIGGER tr_game_round_active_for_clue
    BEFORE INSERT OR UPDATE ON game_round
    FOR EACH ROW
    EXECUTE FUNCTION enforce_active_game_and_round_for_clue();