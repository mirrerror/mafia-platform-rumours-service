-- This script will populate the Rumours table with initial data, if it's empty.
-- If any error occurs, the EXCEPTION block will handle it, and the transaction will be rolled back.

BEGIN;

DO $$
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM "Rumours") THEN
            INSERT INTO "Rumours" ("LobbyId", "Type", "OwnerId", "TargetId", "Text", "CreatedAt")
            VALUES
                ('seed_lobby_1', 'role', 101, 102, 'I heard a whisper that player 102 might be the... well, you know.', NOW() - INTERVAL '10 minutes'),
                ('seed_lobby_1', 'activity', 101, 103, 'Someone saw player 103 sneaking around last night.', NOW() - INTERVAL '8 minutes'),
                ('seed_lobby_1', 'allegiance', 102, 104, 'Player 104 seems unusually friendly with the wrong crowd.', NOW() - INTERVAL '5 minutes'),
                ('seed_lobby_1', 'role', 103, 101, 'I heard a whisper that player 101 might be the... well, you know.', NOW() - INTERVAL '3 minutes'),

                ('seed_lobby_2', 'activity', 201, 202, 'Someone saw player 202 sneaking around last night.', NOW() - INTERVAL '15 minutes'),
                ('seed_lobby_2', 'allegiance', 202, 203, 'Player 203 seems unusually friendly with the wrong crowd.', NOW() - INTERVAL '12 minutes'),
                ('seed_lobby_2', 'role', 203, 204, 'I heard a whisper that player 204 might be the... well, you know.', NOW() - INTERVAL '11 minutes'),
                ('seed_lobby_2', 'activity', 204, 201, 'Someone saw player 201 sneaking around last night.', NOW() - INTERVAL '9 minutes');

            RAISE NOTICE 'Database seeded with initial rumour data.';
        ELSE
            RAISE NOTICE 'Database already contains rumour data. Seeding skipped.';
        END IF;

    EXCEPTION
        WHEN OTHERS THEN
            RAISE NOTICE 'An error occurred: %', SQLERRM;
            RAISE NOTICE 'Transaction is being rolled back.';
            RAISE;
    END;
$$;

COMMIT;