# Task runner (AGENTS.md §7.1). CI runs the same targets.

.PHONY: regtest-up regtest-down regtest-test

# Regtest stack in tools/regtest/compose.yml (README, "Regtest stack").
regtest-up:
	tools/regtest/up.sh

regtest-down:
	tools/regtest/down.sh

# Needs `make regtest-up` first. Fails, rather than skips, when the stack is down.
regtest-test:
	dotnet test --project tests/Freeside.Regtest.Tests -c Release --filter-trait "Category=Regtest" --minimum-expected-tests 1
