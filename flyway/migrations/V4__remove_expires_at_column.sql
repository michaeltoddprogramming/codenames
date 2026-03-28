DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.tables
        WHERE table_name  = 'game_round' AND   table_schema = 'public'
    ) THEN
        RAISE EXCEPTION 'Table "game_round" does not exist';
    END IF;

    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name   = 'game_round' AND   column_name  = 'expires_at' AND   table_schema = 'public'
    ) THEN
        ALTER TABLE game_round DROP COLUMN expires_at;
    END IF;
END;
$$;