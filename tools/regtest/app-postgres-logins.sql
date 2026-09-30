-- Login roles for the regtest stack's app database (compose.yml, app-postgres).
-- Runs after bootstrap-roles.sql, when the volume is first created.
--
-- Regtest only. The passwords are throwaways for this local stack and must never
-- be reused. Other environments use Entra logins (README, "Database and migrations").

CREATE ROLE freeside_migrator_login LOGIN PASSWORD 'regtest-throwaway' IN ROLE freeside_migrator;
CREATE ROLE freeside_app_login LOGIN PASSWORD 'regtest-throwaway' IN ROLE freeside_app;
