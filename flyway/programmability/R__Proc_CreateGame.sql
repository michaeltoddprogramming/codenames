CREATE OR REPLACE PROCEDURE create_game(
    p_words                  JSONB,
    p_match_duration_minutes INT,
    OUT p_game_id            INT
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_game_status_id INT;
    v_word_count     INT;
    v_invalid_types  TEXT;
BEGIN
    IF jsonb_typeof(p_words) <> 'array' THEN
        RAISE EXCEPTION 'p_words must be a JSON array';
    END IF;

    IF jsonb_array_length(p_words) = 0 THEN
        RAISE EXCEPTION 'Word list cannot be empty';
    END IF;

    IF EXISTS (
        SELECT 1 FROM jsonb_array_elements(p_words) AS w
        WHERE w->>'word' IS NULL OR w->>'word_type' IS NULL
    ) THEN
        RAISE EXCEPTION 'Each word entry must have both "word" and "word_type" fields';
    END IF;

    SELECT string_agg(DISTINCT w->>'word_type', ', ')
    INTO v_invalid_types
    FROM jsonb_array_elements(p_words) AS w
    WHERE NOT EXISTS (
        SELECT 1 FROM word_type wt WHERE wt.type_name = w->>'word_type'
    );

    IF v_invalid_types IS NOT NULL THEN
        RAISE EXCEPTION 'Invalid word type(s): %', v_invalid_types;
    END IF;

    SELECT game_status_id INTO v_game_status_id
    FROM game_status
    WHERE game_status = 'active';

    INSERT INTO game (game_status_id, match_duration_minutes, match_started_at, match_ends_at)
    VALUES (
        v_game_status_id,
        p_match_duration_minutes,
        NOW(),
        NOW() + (p_match_duration_minutes || ' minutes')::INTERVAL
    )
    RETURNING game_id INTO p_game_id;

    INSERT INTO game_word (game_id, word_type_id, word)
    SELECT p_game_id, wt.word_type_id, w->>'word'
    FROM jsonb_array_elements(p_words) AS w
    JOIN word_type wt ON wt.type_name = w->>'word_type'
    ON CONFLICT ON CONSTRAINT unique_game_word_details DO NOTHING;

    GET DIAGNOSTICS v_word_count = ROW_COUNT;

    IF v_word_count <> jsonb_array_length(p_words) THEN
        RAISE EXCEPTION 'Some words were not inserted — possible duplicates. Expected %, got %',
            jsonb_array_length(p_words), v_word_count;
    END IF;
END;
$$;
