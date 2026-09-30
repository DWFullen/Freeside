# Task runner (AGENTS.md §7.1). CI runs the same targets.

.PHONY: regtest-up regtest-down regtest-migrate regtest-test

# Regtest stack in tools/regtest/compose.yml (README, "Regtest stack").
regtest-up:
	tools/regtest/up.sh

regtest-down:
	tools/regtest/down.sh

# Applies the EF migrations to the stack's app database, as the migrator login.
# Regtest only: the password is the stack's throwaway.
regtest-migrate:
	dotnet tool restore
	dotnet ef database update --project src/Freeside.Infrastructure \
		--connection "Host=127.0.0.1;Port=55432;Database=freeside;Username=freeside_migrator_login;Password=regtest-throwaway"

# Needs `make regtest-up` first. Fails, rather than skips, when the stack is down.
regtest-test:
	dotnet test --project tests/Freeside.Regtest.Tests -c Release --filter-trait "Category=Regtest" --minimum-expected-tests 1
