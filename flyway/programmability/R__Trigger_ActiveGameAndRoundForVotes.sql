CREATE OR REPLACE FUNCTION enforce_active_game_and_round_for_vote()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM game_round gr
        INNER JOIN game g ON g.game_id = gr.game_id
        INNER JOIN game_status gs ON gs.game_status_id = g.game_status_id
        INNER JOIN round_status rs ON rs.round_status_id = gr.round_status_id
        WHERE gr.game_round_id = NEW.game_round_id
          AND gs.game_status = 'active'
          AND rs.round_status = 'active'
          AND NOT gr.is_deleted
          AND NOT g.is_deleted
    ) THEN
        RAISE EXCEPTION 'Votes are only allowed when the game and round are active.'
            USING ERRCODE = 'check_violation';
    END IF;
    RETURN NEW;
END;
$$;

CREATE OR REPLACE TRIGGER tr_round_vote_active_game_round
    BEFORE INSERT OR UPDATE ON round_vote
    FOR EACH ROW
    EXECUTE FUNCTION enforce_active_game_and_round_for_vote();