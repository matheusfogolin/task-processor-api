# Task Processor API

Projeto de estudo que demonstra processamento assíncrono de tarefas (jobs) utilizando **Clean Architecture**, **CQRS** e **mensageria**. A API recebe requisições de criação de jobs, enfileira via RabbitMQ, e um Worker processa em background com lease atômico no MongoDB.

## Como Executar o Projeto

### Pré-requisitos

- [Docker](https://www.docker.com/) e Docker Compose instalados

### Subindo os containers

```bash
docker compose up -d
```

Isso sobe 4 containers:

| Container | Descrição |
|-----------|-----------|
| `taskprocessor-api` | API REST (ASP.NET Core) |
| `taskprocessor-worker` | Worker (Consumer RabbitMQ + BackgroundService) |
| `taskprocessor-mongodb` | MongoDB 8 |
| `taskprocessor-rabbitmq` | RabbitMQ 4 com Management UI |

### Portas expostas

| Serviço | URL |
|---------|-----|
| API | `http://localhost:8080` |
| RabbitMQ Management | `http://localhost:15672` (guest/guest) |
| MongoDB | `localhost:27017` |

As variáveis de ambiente já estão configuradas no `docker-compose.yml`. Não é necessária nenhuma configuração adicional.

### Outros comandos úteis

```bash
# Parar os containers
docker compose down

# Rebuild sem cache
docker compose build --no-cache

# Logs da API em tempo real
docker compose logs -f api
```

## Tecnologias Utilizadas

| Tecnologia | Uso |
|------------|-----|
| C# / .NET 10 | Linguagem e runtime |
| ASP.NET Core | Framework web (API REST) |
| MongoDB | Banco de dados (persistência de jobs) |
| RabbitMQ | Mensageria (fila de criação de jobs) |
| Docker | Containerização |
| MediatR | Mediator para CQRS (Commands e Queries) |
| FluentValidation | Validação de requests |
| xUnit | Framework de testes |
| Moq | Mocking para testes unitários |
| FluentAssertions | Assertions expressivas nos testes |

## Arquitetura

O projeto segue **Clean Architecture** com dependências apontando para dentro — o Domain não depende de nada externo.

### Camadas

```
Presentation (API)  →  Application  →  Domain  ←  Infrastructure
                                        ↑
                                       Worker
```

### Padrões e decisões

- **CQRS via MediatR** — Commands (escrita) e Queries (leitura) separados. Handlers independentes facilitam testes e evolução.
- **Mensageria com RabbitMQ** — Desacopla a criação do job (API publica mensagem) do processamento (Worker consome). A API responde imediatamente sem esperar o processamento.
- **Lease atômico no MongoDB** — O BackgroundService usa `FindOneAndUpdate` para adquirir jobs com lock atômico (`LockedBy`/`LockedUntil`), evitando processamento duplicado mesmo com múltiplas instâncias do Worker.
- **Result Pattern** — Erros de negócio retornam `Result<T>` ao invés de lançar exceções. Fluxo explícito e previsível.
- **Dockerfile multi-stage** — Um único Dockerfile com targets `api` e `worker`, compartilhando o build stage para otimizar o tamanho da imagem.

## Fluxo da Aplicação

### Criação de um job

```
Cliente  →  POST /api/jobs  →  API publica mensagem no RabbitMQ  →  Retorna 201 com Id

Consumer (Worker)  →  Consome mensagem do RabbitMQ  →  Cria job no MongoDB (status: Pending)
```

### Processamento

```
BackgroundService (Worker)  →  Polling no MongoDB
                            →  Adquire lease atômico (FindOneAndUpdate)
                            →  Status: Pending → Processing
                            →  Processa o job
                            →  Sucesso: Processing → Completed
                            →  Falha: Processing → Failed (incrementa RetryCount, calcula NextRetryAt)
```

### Retry

```
BackgroundService  →  Busca jobs com status Failed
                   →  Condição: RetryCount < MaxRetries E NextRetryAt <= agora
                   →  Failed → Processing (novo lease atômico)
```

### Consulta

```
Cliente  →  GET /api/jobs/{id}  →  Retorna status atualizado do job
```

### Transições de status

```
(criação via Consumer) → Pending
Pending    → Processing   (BackgroundService adquire lease)
Processing → Completed    (sucesso)
Processing → Failed       (falha, incrementa RetryCount)
Failed     → Processing   (retry automático)
```

## Endpoints da API

### `POST /api/jobs`

Cria um novo job.

**Request:**

```json
{
  "type": "email-notification",
  "payload": "{\"to\": \"user@example.com\", \"subject\": \"Test\"}"
}
```

**Response (201 Created):**

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "type": "email-notification"
}
```

### `GET /api/jobs/{id}`

Consulta o status de um job.

**Response (200 OK):**

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "type": "email-notification",
  "payload": "{\"to\": \"user@example.com\", \"subject\": \"Test\"}",
  "status": 0,
  "retryCount": 0,
  "maxRetries": 3,
  "createdAt": "2025-01-01T00:00:00Z",
  "updatedAt": "2025-01-01T00:00:00Z",
  "completedAt": null,
  "errorMessage": null
}
```

### Collection Postman

Uma collection pronta para teste está disponível em [`postman/TaskProcessor.postman_collection.json`](postman/TaskProcessor.postman_collection.json). Importe no Postman para testar todos os endpoints com cenários de sucesso, validação e not found.

## Estrutura do Projeto

```
src/
├── task-processor/              # Presentation — API REST
│   ├── Controllers/             #   Endpoints (JobController)
│   ├── Extensions/              #   ResultExtensions (Result<T> → IActionResult)
│   ├── Middleware/               #   GlobalExceptionMiddleware
│   └── Program.cs               #   Configuração e DI
│
├── TaskProcessor.Application/   # Application — Orquestração
│   ├── Job/
│   │   ├── Create/              #   CreateJobCommand, Handler, Validator, DTOs
│   │   └── GetById/             #   GetJobByIdQuery, Handler, DTOs
│   ├── Shared/                  #   JobSettings, ValidationBehavior
│   └── Messages/                #   CreateJobMessage (mensagem RabbitMQ)
│
├── TaskProcessor.Domain/        # Domain — Regras de negócio
│   ├── Aggregates/JobAggregate/ #   Entidade Job, IJobRepository, EJobStatus
│   ├── Shared/                  #   Result<T>, Error, Errors
│   └── Ports/                   #   IMessageQueuePublisher, IMessageQueueConsumer
│
├── TaskProcessor.Infrastructure/ # Infrastructure — Implementações externas
│   ├── Repositories/            #   JobRepository (MongoDB)
│   ├── Persistence/             #   MongoDbContext
│   └── MessageQueue/            #   RabbitMqPublisher, RabbitMqConsumer
│
├── TaskProcessor.Worker/        # Worker — Processamento em background
│   ├── Consumers/               #   JobCreationConsumer (RabbitMQ → MongoDB)
│   ├── Services/                #   JobProcessingService (polling + lease)
│   └── Program.cs               #   Configuração e DI
│
└── TaskProcessor.Tests/         # Tests — Testes unitários
    ├── Domain/                  #   Testes da entidade Job e Result<T>
    ├── Application/             #   Testes dos Handlers e Validators
    ├── Worker/                  #   Testes do Consumer e BackgroundService
    └── Presentation/            #   Testes do Controller e Middleware
```

## Testes

```bash
# Executar todos os testes
dotnet test

# Executar testes filtrados por nome
dotnet test --filter "FullyQualifiedName~CreateJobHandlerTest"
```

O projeto possui 67 testes unitários cobrindo todas as camadas:

| Camada | O que testa |
|--------|-------------|
| Domain | Entidade Job (criação, transições de status, retry), Result\<T\> |
| Application | CreateJobHandler, GetJobByIdHandler, CreateJobValidator |
| Worker | JobCreationConsumer, JobProcessingService |
| Presentation | JobController, GlobalExceptionMiddleware |

Frameworks: **xUnit** + **Moq** + **FluentAssertions**

## Observações Importantes

- **Lease atômico** — Os campos `LockedBy` e `LockedUntil` são preenchidos exclusivamente pelo MongoDB `FindOneAndUpdate` no repository. A entidade Job não gerencia lease.
- **Result Pattern** — Erros de negócio usam `Result<T>` (em `Domain/Shared/`). Exceções são reservadas para falhas inesperadas.
- **MaxRetries configurável** — Definido em `appsettings.json` na seção `"Job"`, não vem da requisição do cliente. O handler resolve via `IOptions<JobSettings>`.
