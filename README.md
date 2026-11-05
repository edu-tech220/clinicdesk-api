# ClinicDesk API

API REST para la agenda de citas de una clinica: horarios de doctores, generacion automatica
de horarios disponibles, reserva con control de concurrencia, lista de espera con promocion
automatica y politica de cancelacion. Construida con C# / .NET 8, ASP.NET Core Web API,
Entity Framework Core y SQL Server (SQLite en las pruebas).

## Que resuelve

Una clinica necesita que los pacientes reserven citas con un doctor sin pisarse entre si,
que el doctor pueda definir y cambiar su horario semanal sin perder el historial, y que un
horario que se libera (por cancelacion) se ofrezca automaticamente a quien ya estaba
esperando, en vez de quedar disponible para cualquiera por orden de llegada. El dominio
tiene reglas no triviales mas alla de un CRUD:

- **Reserva con concurrencia real**: si dos pacientes intentan tomar el mismo horario casi
  al mismo tiempo, solo uno gana y el otro recibe un 409 para reintentar o unirse a la lista
  de espera.
- **Horarios versionados**: cuando un doctor cambia su horario semanal, las reglas anteriores
  no se borran ni se sobrescriben; se cierran (`EffectiveTo`) y se insertan las nuevas. Los
  horarios ya generados conservan la referencia a la version de horario con la que nacieron.
- **Lista de espera FIFO con promocion automatica**: si un paciente cancela una cita
  confirmada y hay alguien esperando ese horario, se promueve automaticamente al primero en
  la fila en la misma transaccion, en vez de simplemente liberar el horario.
- **Politica de cancelacion**: cancelar con mas de 24 horas de anticipacion (configurable) se
  marca `Cancelled`; cancelar mas tarde se marca `LateCancelled`, para poder medir
  inasistencias efectivas del paciente.
- **Generacion de horarios en segundo plano**: un `BackgroundService` genera los horarios
  disponibles de cada doctor a partir de su horario activo, de forma idempotente (no duplica
  si ya existen).

## Arquitectura

Un unico proyecto de API (`ClinicDesk.Api`) organizado por capas dentro de carpetas, sin
dividir en multiples class libraries porque el alcance no lo justifica:

```
src/ClinicDesk.Api/
  Domain/            Entidades y enums, sin dependencias de EF Core ni ASP.NET
  Application/        Casos de uso (Services), DTOs y excepciones de dominio
  Infrastructure/      DbContext, migraciones, hashing de contrasenas, JWT
  BackgroundServices/  Generador periodico de horarios
  Common/              Middleware de ProblemDetails, opciones, claims, extensiones
  Controllers/         Endpoints HTTP, mapean DTOs <-> servicios y resuelven autorizacion
```

Los controladores no hablan con EF Core directamente: llaman a los servicios de
`Application.Services`, que son las unicas clases que usan `ClinicDeskDbContext`. Las
excepciones de dominio (`NotFoundException`, `ConflictException`, `ValidationException`,
`ForbiddenException`, `UnauthorizedDomainException`) se convierten a `ProblemDetails` en un
middleware central (`ProblemDetailsExceptionMiddleware`), asi los servicios no conocen
codigos HTTP y los controladores no tienen try/catch repetidos.

## Decisiones tecnicas

- **Token de concurrencia propio en vez de `rowversion`**: `AppointmentSlot` y `Appointment`
  implementan `IConcurrencyAware` con una propiedad `Version` (`Guid`) marcada como
  `IsConcurrencyToken()`. `ClinicDeskDbContext` la regenera en cada `SaveChanges` para las
  entidades modificadas. Se eligio esto en vez de `[Timestamp]`/`rowversion` nativo de SQL
  Server porque ese tipo no tiene equivalente real en SQLite, y el requisito es que las
  pruebas corran sobre SQLite sin tocar el comportamiento de concurrencia que se valida en
  produccion contra SQL Server. El mecanismo de deteccion de conflictos (comparar el valor
  original contra el de la base al hacer `UPDATE`) es el mismo en ambos motores.
- **Reglas de horario versionadas, no editadas in place**: `DoctorService.SetScheduleAsync`
  nunca hace `UPDATE` sobre una fila de `DoctorSchedule` existente; cierra la vigente
  (`EffectiveTo`) y crea una nueva. Esto permite que `AppointmentSlot.DoctorScheduleId`
  siga apuntando a la version de horario con la que se genero, util para auditoria.
- **Generacion de slots sin N+1**: `SlotGenerationService` trae en una sola consulta todos
  los `StartsAtUtc` ya existentes para el rango de fechas de un doctor, y evalua la
  pertenencia en memoria (`HashSet`) en vez de preguntar a la base de datos slot por slot
  antes de insertar.
- **Promocion de lista de espera dentro de la misma transaccion de cancelacion**:
  `AppointmentService.CancelAsync` decide en el mismo `SaveChanges` si libera el horario o
  promueve al primero en espera, evitando una ventana donde el horario aparezca disponible
  para terceros.
- **JWT con claims de dominio**: ademas de `sub`, `email` y `role`, el token incluye `pid`
  (PatientId) o `did` (DoctorId) segun el rol, para no tener que resolver ese id en cada
  request con una consulta extra. `MapInboundClaims = false` evita que ASP.NET Core remapee
  los nombres cortos de claims a URIs largas por defecto.
- **PBKDF2 manual en vez de ASP.NET Identity**: el requisito del dominio es simple
  (hash + verificacion), y usar `Rfc2898DeriveBytes.Pbkdf2` directamente evita arrastrar el
  modelo de usuarios de Identity para un caso de uso que no lo necesita.
- **Rate limiting por ventana fija**: los endpoints de autenticacion y de reserva de citas
  tienen una politica de `AddFixedWindowLimiter` para mitigar fuerza bruta sobre login y
  spam de intentos de reserva.

## Modelo de dominio (resumen)

`Specialty` 1—N `Doctor` 1—N `DoctorSchedule` (versionado) → `SlotGenerationService` genera
`AppointmentSlot` → `Patient` reserva un `AppointmentSlot` creando un `Appointment`
(`Confirmed → Completed | Cancelled | LateCancelled | NoShow`). Si el slot esta ocupado, el
paciente puede crear un `WaitlistEntry` (FIFO) que se promueve automaticamente al cancelarse
la cita titular.

## Como correrlo

### Con Docker Compose (API + SQL Server)

```bash
docker compose up --build
```

La API queda disponible en `http://localhost:8080` (Swagger en `/swagger` si
`ASPNETCORE_ENVIRONMENT=Development`). Las credenciales y el `Jwt:SigningKey` en
`docker-compose.yml` y `appsettings.json` son valores de ejemplo para desarrollo local,
**no son secretos reales**; cambialos antes de desplegar a un entorno real.

### Local, sin Docker

Necesitas una instancia de SQL Server accesible (puede ser la del propio `docker compose up
sqlserver`) y el connection string en `src/ClinicDesk.Api/appsettings.Development.json` o en
variables de entorno (`ConnectionStrings__ClinicDesk`).

```bash
dotnet run --project src/ClinicDesk.Api
```

Al arrancar fuera del entorno `Testing`, el propio `Program.cs` aplica las migraciones
pendientes (`db.Database.Migrate()`), asi que no hace falta correr `dotnet ef database
update` a mano.

## Como probarlo

```bash
dotnet test
```

Las pruebas no requieren SQL Server ni Docker: usan SQLite en memoria.

- Las pruebas de servicios (`tests/.../Services`) usan `SqliteInMemoryFixture`, que abre una
  conexion SQLite `:memory:` compartida entre varios `ClinicDeskDbContext` independientes,
  lo que permite simular dos "peticiones" concurrentes leyendo el mismo slot antes de que
  cualquiera guarde cambios (asi se prueba el token de concurrencia de verdad, no solo el
  atajo de negocio por `Status`).
- Las pruebas de integracion (`tests/.../Integration`) levantan la API completa con
  `WebApplicationFactory<Program>` en el entorno `Testing`, en el que `Program.cs` omite el
  registro de SQL Server, la migracion automatica y el `BackgroundService` de generacion de
  horarios, y usan su propia base SQLite en memoria.

Cobertura relevante: concurrencia optimista en la reserva, politica de cancelacion libre vs.
tardia, promocion FIFO de lista de espera, versionado de horarios sin perdida de historial,
generacion idempotente de slots, paginacion, hashing de contrasenas, autorizacion por rol y
formato de errores (`ProblemDetails`).

## Endpoints principales

| Metodo | Ruta | Rol | Descripcion |
|---|---|---|---|
| POST | `/api/auth/register` | Publico | Registra un paciente y devuelve un JWT |
| POST | `/api/auth/login` | Publico | Login, devuelve un JWT |
| POST | `/api/auth/doctors` | Admin | Crea la cuenta de un doctor |
| GET | `/api/specialties` | Publico | Lista especialidades |
| POST | `/api/specialties` | Admin | Crea una especialidad |
| GET | `/api/doctors` | Publico | Lista doctores (filtro por especialidad, paginado) |
| PUT | `/api/doctors/{id}/schedule` | Doctor dueno / Admin | Reemplaza el horario semanal (versionado) |
| POST | `/api/doctors/{id}/generate-slots` | Doctor dueno / Admin | Dispara la generacion manual de horarios |
| GET | `/api/doctors/{id}/slots` | Autenticado | Agenda del doctor (paginada, filtrable por estado y rango) |
| POST | `/api/appointments` | Patient | Reserva un horario disponible |
| POST | `/api/slots/{slotId}/waitlist` | Patient | Se une a la lista de espera de un horario ocupado |
| GET | `/api/appointments` | Autenticado | Lista citas (alcance segun rol: propias o todas si es Admin) |
| PATCH | `/api/appointments/{id}/cancel` | Dueno / Doctor / Admin | Cancela una cita confirmada |
| PATCH | `/api/appointments/{id}/complete` | Doctor dueno / Admin | Marca una cita como atendida |
| PATCH | `/api/appointments/{id}/no-show` | Doctor dueno / Admin | Marca inasistencia |

## Configuracion

| Seccion | Clave | Uso |
|---|---|---|
| `ConnectionStrings` | `ClinicDesk` | Cadena de conexion a SQL Server (no usada en `Testing`) |
| `Jwt` | `Issuer`, `Audience`, `SigningKey`, `ExpirationMinutes` | Firma y validacion de tokens |
| `ClinicPolicy` | `FreeCancellationWindowHours` | Horas de anticipacion para cancelar sin penalizacion |
| `ClinicPolicy` | `SlotGenerationHorizonDays` | Dias hacia adelante que genera el `BackgroundService` |
