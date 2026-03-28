# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/TaskProcessor.Domain/*.csproj TaskProcessor.Domain/
COPY src/TaskProcessor.Application/*.csproj TaskProcessor.Application/
COPY src/TaskProcessor.Infrastructure/*.csproj TaskProcessor.Infrastructure/
COPY src/task-processor/*.csproj task-processor/
COPY src/TaskProcessor.Worker/*.csproj TaskProcessor.Worker/
RUN dotnet restore task-processor/task-processor.csproj
RUN dotnet restore TaskProcessor.Worker/TaskProcessor.Worker.csproj

COPY src/TaskProcessor.Domain/ TaskProcessor.Domain/
COPY src/TaskProcessor.Application/ TaskProcessor.Application/
COPY src/TaskProcessor.Infrastructure/ TaskProcessor.Infrastructure/
COPY src/task-processor/ task-processor/
COPY src/TaskProcessor.Worker/ TaskProcessor.Worker/

RUN dotnet publish task-processor/task-processor.csproj -c Release -o /app/api --no-restore
RUN dotnet publish TaskProcessor.Worker/TaskProcessor.Worker.csproj -c Release -o /app/worker --no-restore

# API runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS api
WORKDIR /app

USER $APP_UID

COPY --from=build /app/api .

EXPOSE 8080
ENTRYPOINT ["dotnet", "task-processor.dll"]

# Worker runtime stage
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS worker
WORKDIR /app

USER $APP_UID

COPY --from=build /app/worker .

ENTRYPOINT ["dotnet", "TaskProcessor.Worker.dll"]
