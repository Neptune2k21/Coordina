start-api:
	dotnet run --project src/api/Coordina.Api.csproj --urls http://localhost:5050

start-full:
	docker compose --env-file .env -f docker/docker-compose.yml -p coordina up -d --build

clean:
	docker compose --env-file .env -f docker/docker-compose.yml -p coordina down -v --rmi all --remove-orphans
