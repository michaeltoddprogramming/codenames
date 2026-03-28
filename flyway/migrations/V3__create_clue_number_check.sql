DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.tables
        WHERE table_name = 'game_round' AND table_schema = 'public'
    ) THEN
        RAISE EXCEPTION 'Table "game_round" does not exist';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'game_round' AND column_name = 'clue_number' AND table_schema = 'public'
    ) THEN
        RAISE EXCEPTION 'Column "clue_number" does not exist on "game_round"';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.table_constraints
        WHERE table_name = 'game_round' AND constraint_name = 'chk_clue_number_positive'
        AND   table_schema = 'public'
    ) THEN
        ALTER TABLE game_round
        ADD CONSTRAINT chk_clue_number_positive CHECK (clue_number >= 1);
    END IF;
END;
$$;

