-- Migration 143: Wipe all currency data due to DateAdded field corruption
-- This migration clears all currency-related tables to fix historical data issues
-- WARNING: This will DELETE all currency balances and transaction history!

-- Wipe transaction history (both guild and global)
DO
$$
    BEGIN
        IF EXISTS (SELECT 1
                   FROM information_schema.tables
                   WHERE table_schema = 'public'
                     AND table_name = 'TransactionHistory') THEN
            TRUNCATE TABLE public."TransactionHistory" RESTART IDENTITY CASCADE;
            RAISE NOTICE 'TransactionHistory table cleared';
        END IF;
    END
$$;

-- Wipe guild-specific user balances
DO
$$
    BEGIN
        IF EXISTS (SELECT 1
                   FROM information_schema.tables
                   WHERE table_schema = 'public'
                     AND table_name = 'GuildUserBalance') THEN
            TRUNCATE TABLE public."GuildUserBalance" RESTART IDENTITY CASCADE;
            RAISE NOTICE 'GuildUserBalance table cleared';
        END IF;
    END
$$;

-- Wipe global user balances
DO
$$
    BEGIN
        IF EXISTS (SELECT 1
                   FROM information_schema.tables
                   WHERE table_schema = 'public'
                     AND table_name = 'GlobalUserBalance') THEN
            TRUNCATE TABLE public."GlobalUserBalance" RESTART IDENTITY CASCADE;
            RAISE NOTICE 'GlobalUserBalance table cleared';
        END IF;
    END
$$;

-- Wipe XP currency rewards to ensure consistency
DO
$$
    BEGIN
        IF EXISTS (SELECT 1
                   FROM information_schema.tables
                   WHERE table_schema = 'public'
                     AND table_name = 'XpCurrencyRewards') THEN
            TRUNCATE TABLE public."XpCurrencyRewards" RESTART IDENTITY CASCADE;
            RAISE NOTICE 'XpCurrencyRewards table cleared';
        END IF;
    END
$$;

-- Reset daily reward configuration in all guilds to force reconfiguration
DO
$$
    BEGIN
        IF EXISTS (SELECT 1
                   FROM information_schema.tables
                   WHERE table_schema = 'public'
                     AND table_name = 'GuildConfigs') THEN
            UPDATE public."GuildConfigs"
            SET "RewardAmount"         = 0,
                "RewardTimeoutSeconds" = 0
            WHERE "RewardAmount" > 0
               OR "RewardTimeoutSeconds" > 0;
            RAISE NOTICE 'Guild daily rewards reset';
        END IF;
    END
$$;

-- Log the migration completion
DO
$$
    BEGIN
        RAISE NOTICE 'Migration 143 completed: All currency data has been wiped';
    END
$$;