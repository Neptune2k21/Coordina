.PHONY: help api api-watch web web-build dev docker-up docker-down clean start-api watch-api run-web start-full

API_PROJECT := src/api/Coordina.Api.csproj
API_URL := http://localhost:5050
WEB_DIR := src/web
DOCKER_COMPOSE := docker compose --env-file .env -f docker/docker-compose.yml -p coordina

help:
	@echo "Commandes disponibles :"
	@echo "  make dev          Lance l'API C# et le front React"
	@echo "  make api          Lance l'API C#"
	@echo "  make api-watch    Lance l'API C# en mode watch"
	@echo "  make web          Lance le front React"
	@echo "  make web-build    Build le front React"
	@echo "  make docker-up    Lance les services Docker"
	@echo "  make docker-down  Stoppe les services Docker"
	@echo "  make clean        Supprime les conteneurs, volumes et images Docker"

api:
	dotnet run --project $(API_PROJECT) --urls $(API_URL)

api-watch:
	dotnet watch --project $(API_PROJECT) --urls $(API_URL)

web:
	pnpm --dir $(WEB_DIR) dev

web-build:
	pnpm --dir $(WEB_DIR) build

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
