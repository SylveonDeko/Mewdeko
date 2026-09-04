-- Read-only preflight for numeric-to-bigint.sql. Safe to run at any time, outside a maintenance window.
--
-- Every remaining snowflake column is numeric(20,0), so its scale is already zero and no fractional value
-- can be lost. The one thing that can go wrong is magnitude: numeric(20,0) holds up to 10^20 - 1 while
-- bigint stops at 9223372036854775807. A Discord snowflake will not reach that until roughly 2084, but
-- counters and bitfields share these columns, so check rather than assume.
--
-- This scans all 507 columns across 208 tables, so expect it to run for a while on the larger tables.
-- It raises an exception listing every offending column, or reports that the conversion is safe.

DO
$$
    DECLARE
        col      RECORD;
        max_val  NUMERIC;
        min_val  NUMERIC;
        offences INT := 0;
    BEGIN
        FOR col IN
            SELECT table_name, column_name
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND data_type = 'numeric'
            ORDER BY table_name, column_name
            LOOP
                EXECUTE FORMAT('SELECT MAX(%I), MIN(%I) FROM %I', col.column_name, col.column_name,
                               col.table_name)
                    INTO max_val, min_val;

                IF max_val > 9223372036854775807 OR min_val < -9223372036854775808 THEN
                    RAISE WARNING 'Out of bigint range: "%"."%" (min %, max %)',
                        col.table_name, col.column_name, min_val, max_val;
                    offences := offences + 1;
                END IF;
            END LOOP;

        IF offences > 0 THEN
            RAISE EXCEPTION '% column(s) do not fit in bigint; do not run the conversion', offences;
        END IF;

        RAISE NOTICE 'All numeric columns fit in bigint, conversion is safe.';
    END
$$;
