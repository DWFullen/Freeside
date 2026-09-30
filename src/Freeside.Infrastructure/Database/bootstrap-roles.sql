-- Group roles for the Freeside app database (docs/plans/phase-0.md, PR 4).
--
-- Run once per database, as its owner or an admin, before the first migration.
-- Safe to run again. Login roles are then made members, for example:
--   GRANT freeside_migrator TO "<migrator login or Entra principal>";
--   GRANT freeside_app TO "<web or worker login or Entra principal>";
--
--   freeside_migrator  applies migrations and owns the tables
--   freeside_app       what the web and worker hosts run as; on ledger_entries
--                      it may only SELECT and INSERT (AGENTS.md §2, invariant 7)

DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'freeside_migrator') THEN
        CREATE ROLE freeside_migrator NOLOGIN;
    END IF;
    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'freeside_app') THEN
        CREATE ROLE freeside_app NOLOGIN;
    END IF;
END
$$;

-- Since Postgres 15, PUBLIC can no longer create objects in the public schema.
GRANT USAGE, CREATE ON SCHEMA public TO freeside_migrator;
GRANT USAGE ON SCHEMA public TO freeside_app;
