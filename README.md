# 🚀 EmailDispatchingConsumer - Flujo de Póliza Electrónica

Servicio *Background Worker* de alto rendimiento orientado a eventos para el procesamiento, validación de reglas de negocio, despacho de notificaciones a asegurados (vía API Pegasus/Softnet) y custodia física de pólizas electrónicas. Desarrollado sobre **.NET 10**.

Este componente actúa como el **consumidor final** dentro del ciclo de emisión digital: se suscribe a RabbitMQ para procesar los eventos emitidos por el firmador digital (**DigitalSigningTaskRunner**). La refactorización consolida y moderniza el servicio legacy *GeneradorCorreoSoftnetService*, eliminando operaciones destructivas de archivos, bloqueos síncronos de hilos y mapeos de red no administrados, garantizando la **integridad física de los documentos** y la protección de datos personales.

---

## 🔄 1. Flujo del Sistema y Comparativa de Arquitectura

Al recibir un evento de documento firmado desde RabbitMQ, el servicio procesa de forma aislada y atómica la notificación del asegurado y la custodia física del PDF.

### 🌊 Pipeline de Ejecución E2E

```text
[🐇 RabbitMQ: e-policy.signed.queue ]
               │ (Evento: DocumentProcessedEvent)
               ▼
   [ ⚙️ HostedService (.NET 10 Worker) ] ─── (Crea IServiceScope por mensaje)
               │
               ▼
   [ 🔀 ProcessEmailDispatchHandler ] (Orquestador CQRS)
               │
               ├──► 1. EnsureRoutesLoadedAsync  ──► 🗄️ Oracle DB (Cacheo concurrente bajo SemaphoreSlim)
               │
               ├──► 2. ReadNetworkFileBytes     ──► 📁 NAS [FIRMADOS] (Lectura binaria vía SMB)
               │
               ├──► 3. BuildPegasusPayload      ──► 🧮 Lógica de Negocio y Metadatos (Oracle DB)
               │
               ├──► 4. DispatchEmailNotification──► 🌐 API Pegasus (OAuth2 + Notificación POST)
               │
               └──► 5. ArchiveDocumentAction    ──► 🚚 NAS (Validación de Integridad y Traslado)
                         │
                         ├── Éxito: 📁 NAS [NOTIFICADOS/yyyy-MM-dd] ──► (ACK a RabbitMQ)
                         └── Fallo: 📁 NAS [ERROR_NO_NOTIFICADOS/yyyy-MM-dd] ──► (ACK resguardado / NACK)

```

### ⚠️ Superación del Sistema Legado (*GeneradorCorreoSoftnet*)

El ecosistema anterior presentaba puntos críticos de falla que arriesgaban la pérdida irreversible de archivos:

| Dimensión Técnica | Ecosistema Legado (`GeneradorCorreoSoftnet`) | Nueva Arquitectura CQRS (.NET 10) |
| --- | --- | --- |
| **Integridad de Archivos** | **Destructivo**: `fi.Delete()` ciego tras fallos o en rutinas `LimpiarCarpeta()`. | **Fallo Seguro**: Verificación estricta de bytes antes del borrado. Archivo a `ERROR_NO_NOTIFICADOS`. |
| **Orquestación y Tiempos** | `Thread.Sleep` bloqueante (hasta 4 min) basado en peso de archivo dentro de un *Timer*. | **100% Asíncrono no bloqueante** orientado a eventos vía RabbitMQ. |
| **Seguridad de Datos** | Riesgo de cruce de pólizas por parsing frágil de CSVs compartidos. | **Búsqueda Estricta**: Sin match exacto en Oracle, el PDF se desvía a la carpeta de errores sin procesar. |
| **Acceso a NAS (Red)** | Dependencia de disco local intermedio (`C:\SBM\Pub`) y mapeo COM `mpr.dll`. | **Cliente SMB nativo (`SMBLibrary`)** directo sobre TCP (puerto 445). |
| **Estructura en Disco** | Saturación de archivos en un único directorio compartido. | **Particionamiento Dinámico**: Subcarpetas automáticas por día (`yyyy-MM-dd`). |

---

## 🏛️ 2. Arquitectura de Software y Patrones de Diseño

La solución desacopla la lógica de negocio de los mecanismos de entrega, almacenamiento y comunicación mediante **Clean Architecture** y **CQRS (MediatR)**:

```text
EmailDispatchingConsumer.sln
│
├── 📂 EmailDispatchingConsumer (Host / Windows Service)
│   ├── HostedService.cs               # Consumidor reactivo, Control de Scopes y resolución de Ack/Nack
│   ├── Program.cs                     # Host Builder, Registro de Windows Service y DI Pipeline
│   └── appsettings.json               # Configuración central (Oracle, NAS, Pegasus, RabbitMQ)
│
├── 📂 EmailDispatchingConsumer.Application (Capa de Aplicación - CQRS)
│   ├── Actions/                       # Macro-Actions atómicas (BuildPayload, ExecuteDispatch, Relocate)
│   ├── Configuration/                 # Extensiones de IHostApplicationBuilder (DI y Serilog)
│   └── UseCases/                      # Orquestador (ProcessEmailDispatchHandler) y Warmup
│
├── 📂 EmailDispatchingConsumer.Core (Capa de Dominio)
│   ├── Constants/                     # Diccionario de Mensajes, BD, HTTP y Logs estructurados
│   ├── Dto/ & Models/                 # TramaEnvio, DocumentProcessedEvent, Settings fuertemente tipados
│   └── Interfaces/                    # Contratos de abstracción (IRepository, IStorageService, etc.)
│
└── 📂 EmailDispatchingConsumer.Infrastructure (Capa de Infraestructura)
    ├── Persistence/                   # Dapper, ODP.NET, OracleParameters para RefCursors
    └── Services/                      # SMBLibrary, RabbitMQ Client v7, HttpClient Pegasus y Serilog

```

---

## 🛡️ 3. Resiliencia, Concurrencia y Calidad de Código

1. **Aislamiento Scoped en BackgroundService**:
Con `prefetchCount = 1`, el consumidor procesa un único mensaje a la vez. Cada evento crea explícitamente un `IServiceScope`, liberando memoria RAM, instancias de handlers y conexiones Oracle (`ODP.NET`) al concluir la tarea.
2. **Doble Comprobación de Arranque (*Double-Checked Locking*)**:
El orquestador `ProcessEmailDispatchHandler` protege la carga de rutas desde base de datos con un `SemaphoreSlim(1, 1)`. Esto garantiza que, ante ráfagas de mensajes concurrentes al iniciar, las rutas NAS se consulten y preparen una única vez.
3. **Custodia Física e Integridad SMB**:
En `StorageService.cs`, el método `MoveNetworkFile` escribe el archivo en destino y **valida mediante `GetFileInformation` que el tamaño en bytes coincida de manera exacta** con el payload original (`ValidateFileSizePrivate`). Solo tras confirmar la integridad se emite la orden de eliminación en origen; si falla, el archivo truncado en destino se purga y el original permanece a salvo.
4. **Protección contra "HTTP 200 Falso Positivo"**:
El despacho hacia la API de Pegasus no se limita a verificar `response.IsSuccessStatusCode`. Analiza el cuerpo JSON mediante `JsonDocument` para validar la propiedad booleana `estado`, capturando rechazos de negocio devueltos bajo status HTTP 200.

---

## 🛠️ 4. Stack Tecnológico

| Categoría | Tecnología / Librería | Versión | Propósito |
| --- | --- | --- | --- |
| ⚡ **Runtime** | **.NET 10.0** | `10.0` | Framework base (`net10.0`) con C# 14, `ImplicitUsings` y `Nullable`. |
| 🖥️ **Host / Service** | **Microsoft.Extensions.Hosting.WindowsServices** | `10.0.12` | Integración nativa del Worker para ejecutarse como Servicio de Windows. |
| 🔀 **Arquitectura** | **MediatR** | `12.5.0` | Implementación del patrón Mediator, desacoplamiento CQRS y pipeline de acciones. |
| 🛡️ **Validación** | **FluentValidation** | `12.1.1` | Validación tipada de comandos, eventos y modelos de entrada. |
| 🛡️ **DI Validation** | **FluentValidation.DependencyInjectionExtensions** | `12.1.1` | Inyección automática de validadores en el contenedor de servicios. |
| 🗄️ **Base de Datos** | **Oracle.ManagedDataAccess.Core** | `23.26.301` | Driver ODP.NET gestionado para conectividad de alto rendimiento con Oracle DB. |
| 🚀 **ORM** | **Dapper** | `2.1.89` | Micro-ORM con interceptores para resolución automática de `OracleDbType.RefCursor`. |
| 📁 **Almacenamiento Red** | **SMBLibrary** | `1.5.8.1` | Cliente SMB1/SMB2/SMB3 nativo sobre TCP (sin dependencia de `mpr.dll`). |
| 🐇 **Mensajería** | **RabbitMQ.Client** | `7.2.2` | Broker de mensajería asíncrono con control manual de `BasicAck` / `BasicNack`. |
| 📝 **Logging** | **Serilog.AspNetCore** | `10.0.0` | Logging estructurado hacia consola y archivos físicos rotativos. |
| 🔄 **JSON Engine** | **Microsoft.AspNetCore.Mvc.NewtonsoftJson** | `10.0.13` | Formateo y serialización estricta de payloads para consumo de APIs externas. |
| 🔐 **Seguridad** | **System.Security.Cryptography.Pkcs** | `10.0.13` | Soporte de cifrado, desencapsulado y validación criptográfica de certificados. |

---

## ⚙️ 5. Configuración del Entorno (`appsettings.json`)

```json
{
  "ConnectionStrings": {
    "Database": "Data Source=<DB_HOST>:<PORT>/<SERVICE>;User Id=<USER>;Password=<PASS>;"
  },
  "ConsumerSettings": {
    "LogDirectoryPath": "C:\\Logs\\EmailDispatchingConsumer\\consumer-.log"
  },
  "NASSettings": {
    "Host": "<NAS_HOST_IP>",
    "Domain": "PROTECTA",
    "Username": "<NAS_USER>",
    "Password": "<NAS_PASSWORD>",
    "ShareFolder": "PRD"
  },
  "ApiPegasusSettings": {
    "TokenUrl": "https://intermediacion.pegasus.com.pe/intermediacion-api/oauth/token",
    "NotificacionUrl": "https://intermediacion.pegasus.com.pe/intermediacion-api/notificacion/registrarNotificacion",
    "AuthHeader": "Basic <BASE64_AUTH_OAUTH2>",
    "Username": "<OAUTH_USER>",
    "Password": "<OAUTH_PASSWORD>"
  },
  "RabbitMQSettings": {
    "Host": "<RABBITMQ_HOST>",
    "Port": 5672,
    "VirtualHost": "/",
    "Username": "EmailDispatcher",
    "Password": "<RABBIT_PASS>",
    "ExchangeName": "e-policy.exchange",
    "RoutingKey": "e-policy.signed",
    "QueueName": "e-policy.signed.queue"
  }
}

```

---

## 📦 6. Despliegue en Producción (Windows Service)

1. **Compilar y publicar en modo Release**:

```bash
dotnet publish EmailDispatchingConsumer/EmailDispatchingConsumer.csproj -c Release -o C:\Services\EmailDispatchingConsumer

```

2. **Registrar el servicio con `sc.exe**`:

```cmd
sc.exe create "E-policy - Email Dispatching Consumer" binPath= "C:\Services\EmailDispatchingConsumer\EmailDispatchingConsumer.exe" start= auto

```

3. **Iniciar el Servicio**:

```cmd
net start "E-policy - Email Dispatching Consumer"

```

---

## 🚦 7. Guía de Ejecución Local

1. Contar con el **.NET 10 SDK** instalado.
2. Contar con acceso de red a Oracle DB, servidor NAS (puerto TCP 445) y RabbitMQ.
3. Restaurar paquetes, compilar y ejecutar:

```bash
dotnet restore
dotnet build
dotnet run --project EmailDispatchingConsumer/EmailDispatchingConsumer.csproj

```
