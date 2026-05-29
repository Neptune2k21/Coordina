.PHONY: help api api-restore api-tools api-build api-format api-format-check api-test api-migration api-migrate web web-build web-format web-format-check web-lint web-test web-e2e web-typecheck test lint format format-check quality dev docker-up docker-down clean start-api watch-api run-web start-full

SOLUTION := Coordina.sln
API_PROJECT := src/api/Coordina.Api.csproj
API_URL := http://localhost:5050
WEB_DIR := src/web
DOCKER_COMPOSE := docker compose --env-file .env -f docker/docker-compose.yml -p coordina

help:
	@echo "Commandes disponibles :"
	@echo "  make dev          Lance l'API C# et le front React"
	@echo "  make api          Lance l'API C#"
	@echo "  make api-watch    Lance l'API C# en mode watch"
	@echo "  make api-restore  Restaure les dependances .NET"
	@echo "  make api-tools    Restaure les outils .NET locaux"
	@echo "  make api-migration name=Nom  Cree une migration EF Core"
	@echo "  make api-migrate  Applique les migrations EF Core"
	@echo "  make api-test     Lance les tests .NET"
	@echo "  make web          Lance le front React"
	@echo "  make web-build    Build le front React"
	@echo "  make web-test     Lance les tests React"
	@echo "  make web-e2e      Lance les tests end-to-end Playwright"
	@echo "  make lint         Lance les linters"
	@echo "  make test         Lance tous les tests"
	@echo "  make quality      Lance format-check, lint, tests et builds"
	@echo "  make docker-up    Lance les services Docker"
	@echo "  make docker-down  Stoppe les services Docker"
	@echo "  make clean        Supprime les conteneurs, volumes et images Docker"

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

api-test: api-restore
	dotnet test $(SOLUTION) --configuration Release --no-restore -m:1 --collect:"XPlat Code Coverage"

api-migration: api-restore api-tools
	dotnet tool run dotnet-ef migrations add $(name) --project $(API_PROJECT) --output-dir infrastructure/persistence/Migrations --namespace Coordina.Api.Infrastructure.Persistence.Migrations

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

web-e2e: api-migrate
	pnpm --dir $(WEB_DIR) e2e

web-typecheck:
	pnpm --dir $(WEB_DIR) typecheck

test: api-test web-test web-e2e

lint: api-format-check web-lint web-typecheck

format: api-format web-format

format-check: api-format-check web-format-check

quality: format-check lint test api-build web-build

dev:
	$(MAKE) -j2 api-watch web

docker-up:
	$(DOCKER_COMPOSE) up -d --build

docker-down:
	$(DOCKER_COMPOSE) down --remove-orphans

clean:
	$(DOCKER_COMPOSE) down -v --rmi all --remove-orphans

start-api: api
watch-api: api-watch
run-web: web
start-full: docker-up
