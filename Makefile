.PHONY: help cpp-tools graph-engine-format graph-engine-build graph-engine-test graph-engine-abi graph-engine-abi-check graph-engine-sanitize graph-engine-benchmark graph-engine-benchmark-smoke graph-engine-lab api api-restore api-tools api-build api-format api-format-check api-test api-test-unit api-test-integration api-migration api-migrate web web-build web-format web-format-check web-lint web-test web-e2e web-typecheck docs test lint format format-check quality dev docker-up docker-down docker-logs docker-test-up docker-test-down docker-build clean start-api watch-api run-web start-full check check-all check-staged affected verify suite quality-baseline quality-rules quality-test hooks contract web-contract-check

.PHONY: api-migrations-check e2e-api e2e-db

SOLUTION := Coordina.sln
API_PROJECT := src/api/Coordina.Api.csproj
API_UNIT_TEST_PROJECT := tests/api-unit/Coordina.Api.Unit.Tests.csproj
API_INTEGRATION_TEST_PROJECT := tests/api-integration/Coordina.Api.Integration.Tests.csproj
QUALITY_PROJECT := tools/quality/Coordina.Quality/Coordina.Quality.csproj
QUALITY_TEST_PROJECT := tests/quality/Coordina.Quality.Tests.csproj
QUALITY := dotnet run --project $(QUALITY_PROJECT) --configuration Release --
API_URL := http://localhost:5050
WEB_DIR := src/web
DOCS_DIR := docs
DOCKER_COMPOSE := docker compose --env-file .env -f docker/compose.yml
DOCKER_COMPOSE_TEST := docker compose -f docker/compose.test.yml
API_IMAGE := coordina-api
POSTGRES_TEST_PORT ?= 5433
export POSTGRES_TEST_PORT
E2E_API_URL := http://127.0.0.1:5051
E2E_DATABASE := coordina_e2e
# Everything the API under test reads, so a run never depends on .env.
E2E_ENV := POSTGRES_HOST=127.0.0.1 POSTGRES_PORT=$(POSTGRES_TEST_PORT) POSTGRES_DB=$(E2E_DATABASE) POSTGRES_USER=coordina_test POSTGRES_PASSWORD=coordina_test Auth__Issuer=Coordina.Api Auth__Audience=Coordina.Web Auth__SigningKey=end-to-end-tests-only-signing-key-0123456789abcdef Auth__AccessTokenMinutes=15 Auth__RefreshTokenDays=14 RateLimiting__Auth__PermitLimit=10000
GRAPH_ENGINE_DIR := src/graph-engine
GRAPH_ENGINE_OUT := $(GRAPH_ENGINE_DIR)/bin
GRAPH_ENGINE_TEST_OUT := tests/graph-engine/bin
GRAPH_ENGINE_SOURCES := $(sort $(wildcard $(GRAPH_ENGINE_DIR)/src/*/*.cpp))
GRAPH_ENGINE_FLAGS := -std=c++20 -O2 -fPIC -pthread -Wall -Wextra -Wpedantic -Werror -I$(GRAPH_ENGINE_DIR)/include -I$(GRAPH_ENGINE_DIR)/src
GRAPH_ENGINE_OBJECTS := $(GRAPH_ENGINE_SOURCES:$(GRAPH_ENGINE_DIR)/src/%.cpp=$(GRAPH_ENGINE_OUT)/obj/%.o)
GRAPH_ENGINE_SANITIZE_OBJECTS := $(GRAPH_ENGINE_SOURCES:$(GRAPH_ENGINE_DIR)/src/%.cpp=$(GRAPH_ENGINE_OUT)/sanitized/%.o)
# The native targets compile their objects through this, on every core.
NATIVE_MAKE := $(MAKE) --no-print-directory -j$(shell nproc)
GRAPH_ENGINE_SANITIZE_FLAGS := $(filter-out -O2,$(GRAPH_ENGINE_FLAGS)) -O1 -g -fno-omit-frame-pointer -fsanitize=address,undefined -fno-sanitize-recover=all
GRAPH_ENGINE_TESTS := graph_engine_tests planning_tests numeric_tests duration_tests schedule_tests forecast_tests scope_tests staffing_tests action_tests
GRAPH_ENGINE_ABI := contracts/graph-engine-abi.json
CLANG_TOOLS_DIR := .tools/clang
CLANG_TOOLS_REQUIREMENTS := tools/clang/requirements.txt
CLANG_FORMAT := $(CLANG_TOOLS_DIR)/clang_format/data/bin/clang-format

help:
	@echo "Commandes disponibles :"
	@echo "  make dev          Lance l'infra de dev, l'API C# et le front React"
	@echo "  make graph-engine-test Lance les tests natifs du moteur de graphe C++"
	@echo "  make graph-engine-sanitize Lance les tests natifs sous AddressSanitizer et UBSan"
	@echo "  make graph-engine-format Formate le C++ du moteur avec clang-format"
	@echo "  make cpp-tools    Installe clang-format et clang-tidy aux versions de tools/clang/requirements.txt"
	@echo "  make graph-engine-abi Regenere contracts/graph-engine-abi.json depuis le header C"
	@echo "  make graph-engine-benchmark Mesure l'analyse de board native (sizes=\"100 1000\" optionnel)"
	@echo "  make graph-engine-lab Mesure la justesse des previsions et des conseils sur des equipes simulees"
	@echo "  make api          Lance l'API C#"
	@echo "  make api-watch    Lance l'API C# en mode watch"
	@echo "  make api-restore  Restaure les dependances .NET"
	@echo "  make api-tools    Restaure les outils .NET locaux"
	@echo "  make api-migration name=Nom  Cree une migration EF Core"
	@echo "  make api-migrations-check Verifie les changements de modele sans migration"
	@echo "  make api-migrate  Applique les migrations EF Core"
	@echo "  make api-test     Lance tous les tests .NET (unitaires + integration)"
	@echo "  make api-test-unit Lance les tests unitaires .NET, sans Docker"
	@echo "  make api-test-integration Lance les tests de l'API sur la base de test (Docker requis)"
	@echo "  make web          Lance le front React"
	@echo "  make web-build    Build le front React"
	@echo "  make web-test     Lance les tests React"
	@echo "  make web-e2e      Lance les tests end-to-end Playwright sur la base de test (args=\"--ui\" optionnel)"
	@echo "  make lint         Lance les linters"
	@echo "  make test         Lance tous les tests"
	@echo "  make docs         Lance la documentation Mintlify"
	@echo "  make quality      Lance format-check, lint, verification des migrations, tests et builds"
	@echo "  make check        Verifie les regles qualite sur tes changements (depuis origin/master)"
	@echo "  make check-all    Verifie les regles qualite sur tout le depot"
	@echo "  make affected     Liste les suites impactees par tes changements, et pourquoi"
	@echo "  make verify       Lance seulement les suites impactees (skip=e2e pour les ignorer)"
	@echo "  make quality-rules Liste les regles qualite et leur raison d'etre"
	@echo "  make quality-baseline Reecrit la baseline des erreurs connues"
	@echo "  make hooks        Active les hooks git (pre-commit et commit-msg)"
	@echo "  make contract     Regenere contracts/openapi.json et les types web apres un changement d'API"
	@echo "  make docker-up    Lance l'infra de dev (PostgreSQL, Adminer) et attend qu'elle soit prete"
	@echo "  make docker-down  Stoppe l'infra de dev, sans perdre les donnees"
	@echo "  make docker-logs  Suit les logs de l'infra de dev"
	@echo "  make docker-test-up Lance la base de test (les cibles de test le font deja)"
	@echo "  make docker-test-down Stoppe et vide la base de test"
	@echo "  make docker-build Construit l'image de production de l'API"
	@echo "  make clean        Supprime les conteneurs, volumes et images Docker, donnees de dev comprises"

# clang-format and clang-tidy at the versions the quality tool expects, so
# every machine formats and analyses the C++ the same way. The copy of the
# requirements records what is installed; comparing contents rather than
# dates survives a fresh checkout and the CI cache.
cpp-tools:
	@cmp -s $(CLANG_TOOLS_REQUIREMENTS) $(CLANG_TOOLS_DIR)/requirements.txt || { \
		rm -rf $(CLANG_TOOLS_DIR) && \
		python3 -m pip install --quiet --disable-pip-version-check --target $(CLANG_TOOLS_DIR) -r $(CLANG_TOOLS_REQUIREMENTS) && \
		cp $(CLANG_TOOLS_REQUIREMENTS) $(CLANG_TOOLS_DIR)/requirements.txt; }

graph-engine-format: cpp-tools
	git ls-files --cached --others --exclude-standard -- '$(GRAPH_ENGINE_DIR)/*.cpp' '$(GRAPH_ENGINE_DIR)/*.h' 'tests/graph-engine/*.cpp' 'tests/graph-engine/*.h' | xargs $(CLANG_FORMAT) -i

$(GRAPH_ENGINE_OUT)/obj/%.o: $(GRAPH_ENGINE_DIR)/src/%.cpp
	@mkdir -p $(dir $@)
	g++ $(GRAPH_ENGINE_FLAGS) -MMD -MP -c $< -o $@

$(GRAPH_ENGINE_OUT)/sanitized/%.o: $(GRAPH_ENGINE_DIR)/src/%.cpp
	@mkdir -p $(dir $@)
	g++ $(GRAPH_ENGINE_SANITIZE_FLAGS) -MMD -MP -c $< -o $@

$(GRAPH_ENGINE_TEST_OUT)/%: tests/graph-engine/%.cpp $(GRAPH_ENGINE_OBJECTS)
	@mkdir -p $(GRAPH_ENGINE_TEST_OUT)
	g++ $(GRAPH_ENGINE_FLAGS) -MMD -MP -MF $@.d $< $(GRAPH_ENGINE_OBJECTS) -o $@

$(GRAPH_ENGINE_TEST_OUT)/%_sanitized: tests/graph-engine/%.cpp $(GRAPH_ENGINE_SANITIZE_OBJECTS)
	@mkdir -p $(GRAPH_ENGINE_TEST_OUT)
	g++ $(GRAPH_ENGINE_SANITIZE_FLAGS) -MMD -MP -MF $@.d $< $(GRAPH_ENGINE_SANITIZE_OBJECTS) -o $@

# Kept between runs: make would delete objects only a pattern rule needs.
.SECONDARY: $(GRAPH_ENGINE_OBJECTS) $(GRAPH_ENGINE_SANITIZE_OBJECTS)

-include $(GRAPH_ENGINE_OBJECTS:.o=.d) $(GRAPH_ENGINE_SANITIZE_OBJECTS:.o=.d) $(wildcard $(GRAPH_ENGINE_TEST_OUT)/*.d)

$(GRAPH_ENGINE_OUT)/libcoordina_graph_engine.so: $(GRAPH_ENGINE_OBJECTS)
	g++ $(GRAPH_ENGINE_FLAGS) -shared $(GRAPH_ENGINE_OBJECTS) -o $@

graph-engine-build:
	$(NATIVE_MAKE) $(GRAPH_ENGINE_OUT)/libcoordina_graph_engine.so

graph-engine-test: graph-engine-abi-check
	$(NATIVE_MAKE) $(GRAPH_ENGINE_TESTS:%=$(GRAPH_ENGINE_TEST_OUT)/%)
	$(foreach test,$(GRAPH_ENGINE_TESTS),$(GRAPH_ENGINE_TEST_OUT)/$(test) &&) true

graph-engine-sanitize:
	$(NATIVE_MAKE) $(GRAPH_ENGINE_TESTS:%=$(GRAPH_ENGINE_TEST_OUT)/%_sanitized)
	$(foreach test,$(GRAPH_ENGINE_TESTS),ASAN_OPTIONS=detect_leaks=1:abort_on_error=1 UBSAN_OPTIONS=print_stacktrace=1 $(GRAPH_ENGINE_TEST_OUT)/$(test)_sanitized &&) true

graph-engine-abi:
	mkdir -p $(GRAPH_ENGINE_TEST_OUT)
	g++ $(GRAPH_ENGINE_FLAGS) tests/graph-engine/abi_layout.cpp -o $(GRAPH_ENGINE_TEST_OUT)/abi_layout
	$(GRAPH_ENGINE_TEST_OUT)/abi_layout > $(GRAPH_ENGINE_ABI)

graph-engine-abi-check:
	mkdir -p $(GRAPH_ENGINE_TEST_OUT)
	g++ $(GRAPH_ENGINE_FLAGS) tests/graph-engine/abi_layout.cpp -o $(GRAPH_ENGINE_TEST_OUT)/abi_layout
	@$(GRAPH_ENGINE_TEST_OUT)/abi_layout | diff -u $(GRAPH_ENGINE_ABI) - \
		|| (echo "$(GRAPH_ENGINE_ABI) is out of date. Run 'make graph-engine-abi' and commit the result." && exit 1)

graph-engine-benchmark:
	$(NATIVE_MAKE) $(GRAPH_ENGINE_TEST_OUT)/graph_engine_benchmarks
	$(GRAPH_ENGINE_TEST_OUT)/graph_engine_benchmarks $(sizes)

graph-engine-lab:
	$(NATIVE_MAKE) $(GRAPH_ENGINE_TEST_OUT)/lab
	$(GRAPH_ENGINE_TEST_OUT)/lab

graph-engine-benchmark-smoke:
	$(MAKE) graph-engine-benchmark sizes="100 500 1000"

api: api-migrate
	dotnet run --project $(API_PROJECT) --urls $(API_URL)

api-watch: api-migrate
	dotnet watch --project $(API_PROJECT) --urls $(API_URL)

api-restore:
	dotnet restore $(SOLUTION) -m:1

api-tools:
	dotnet tool restore

api-build: api-restore
	dotnet build $(SOLUTION) --configuration Release --no-restore -m:1

api-format: api-restore
	dotnet format $(SOLUTION) --no-restore

api-format-check: api-restore
	dotnet format $(SOLUTION) --no-restore --verify-no-changes

api-test: api-restore docker-test-up
	dotnet test $(SOLUTION) --configuration Release --no-restore -m:1 --collect:"XPlat Code Coverage"

api-test-unit: api-restore
	dotnet test $(API_UNIT_TEST_PROJECT) --configuration Release --no-restore -m:1

api-test-integration: api-restore docker-test-up
	dotnet test $(API_INTEGRATION_TEST_PROJECT) --configuration Release --no-restore -m:1

api-migrations-check: api-restore api-tools
	dotnet tool run dotnet-ef migrations has-pending-model-changes --project $(API_PROJECT)

api-migration: api-restore api-tools
	dotnet tool run dotnet-ef migrations add $(name) --project $(API_PROJECT) --output-dir core/Persistence/Migrations --namespace Coordina.Api.Core.Persistence.Migrations

api-migrate: api-restore api-tools
	dotnet tool run dotnet-ef database update --project $(API_PROJECT)

web:
	pnpm --dir $(WEB_DIR) dev

web-build:
	pnpm --dir $(WEB_DIR) build

web-format:
	pnpm --dir $(WEB_DIR) format

web-format-check:
	pnpm --dir $(WEB_DIR) format:check

web-lint:
	pnpm --dir $(WEB_DIR) lint

web-test:
	pnpm --dir $(WEB_DIR) test

# Playwright starts the API with e2e-api.
web-e2e:
	pnpm --dir $(WEB_DIR) e2e $(args)

# A new, migrated database on the test server for each run.
e2e-db: docker-test-up api-restore api-tools
	$(DOCKER_COMPOSE_TEST) exec -T db-test dropdb --username coordina_test --force --if-exists $(E2E_DATABASE)
	$(E2E_ENV) dotnet tool run dotnet-ef database update --project $(API_PROJECT)

e2e-api: e2e-db
	$(E2E_ENV) dotnet run --project $(API_PROJECT) --urls $(E2E_API_URL)

web-typecheck:
	pnpm --dir $(WEB_DIR) typecheck

docs:
	cd $(DOCS_DIR) && npx mint dev

test: graph-engine-test graph-engine-sanitize graph-engine-benchmark-smoke api-test web-test web-e2e

lint: api-format-check web-contract-check web-typecheck check-all

format: api-format web-format graph-engine-format

format-check: api-format-check web-format-check

quality: format-check lint api-migrations-check test api-build web-build

check:
	$(QUALITY) check

check-all:
	$(QUALITY) check --all

check-staged:
	$(QUALITY) check --staged

affected:
	$(QUALITY) affected

verify:
	$(QUALITY) verify $(if $(skip),$(foreach suite,$(skip),--skip $(suite)))

suite:
	$(QUALITY) verify --only $(name)

quality-baseline:
	$(QUALITY) baseline

quality-rules:
	$(QUALITY) rules

quality-test:
	dotnet test $(QUALITY_TEST_PROJECT) --configuration Release -m:1

contract: docker-test-up
	UPDATE_API_CONTRACT=1 dotnet test $(API_INTEGRATION_TEST_PROJECT) --configuration Release -m:1 --filter "FullyQualifiedName~ApiContractTests"
	pnpm --dir $(WEB_DIR) contract

web-contract-check:
	pnpm --dir $(WEB_DIR) contract
	git diff --exit-code -- $(WEB_DIR)/src/types/api-contract.ts || (echo "src/web/src/types/api-contract.ts is out of date. Run 'make contract' and commit the result." && exit 1)

hooks:
	git config core.hooksPath .githooks
	@echo "Hooks enabled: pre-commit runs 'make check-staged', commit-msg checks the message."

dev: docker-up
	$(MAKE) -j2 api-watch web

docker-up:
	$(DOCKER_COMPOSE) up -d --wait

docker-down:
	$(DOCKER_COMPOSE) down --remove-orphans

docker-logs:
	$(DOCKER_COMPOSE) logs --follow

docker-test-up:
	$(DOCKER_COMPOSE_TEST) up -d --wait

# The data is in memory, so it goes with the container.
docker-test-down:
	$(DOCKER_COMPOSE_TEST) down --remove-orphans

docker-build:
	docker build -f docker/api/Dockerfile -t $(API_IMAGE) .

clean: docker-test-down
	$(DOCKER_COMPOSE) down -v --rmi all --remove-orphans

start-api: api
watch-api: api-watch
run-web: web
start-full: docker-up

