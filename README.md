# 🚀 EmailDispatchingConsumer - Flujo de Póliza Electrónica

Servicio *Background Worker* de alto rendimiento para el consumo reactivo de eventos, extracción de documentos digitales desde almacenamiento en red (NAS), ensamblado de tramas y despacho masivo de notificaciones por correo electrónico vía API REST (**Pegasus / Softnet**), desarrollado sobre **.NET 10**.

Este componente representa la **etapa final de cierre** dentro del **Flujo de Póliza Electrónica**. Opera desacoplado y en sincronía con **DigitalSigningTaskRunner**, consumiendo de forma controlada la cola de mensajería asíncrona sobre **RabbitMQ** para procesar los documentos firmados y trasladarlos de manera segura hacia las carpetas definitivas (`Notificados` o `ErrorNoNotificados`).

---

## 🔄 1. Flujo de Póliza Electrónica (Fase de Notificación y Cierre)

Tras la generación por **FileProcessor API** y el firmado digital por **DigitalSigningTaskRunner**, el documento queda depositado en la carpeta `Firmados` del NAS y se emite un evento `DocumentProcessedEvent`. A partir de este punto, **EmailDispatchingConsumer** asume la responsabilidad final:

1. **Consumo Asíncrono Reactivo (RabbitMQ)**: Escucha reactiva en la cola `e-policy.signed.queue` con control de concurrencia equitativa (`PrefetchCount = 1`).
2. **Warmup & Validación de Rutas NAS**: Verificación en caché de las rutas parametrizadas en Oracle DB.
3. **Lectura Binaria en Red**: Acceso autenticado en ámbito protegido (`RunInNetworkScope`) al NAS para cargar los bytes del PDF firmado.
4. **Ensamblado de Payload (`TramaEnvio`)**: Construcción de los metadatos a partir del evento, eliminando la dependencia de archivos CSV residuales.
5. **Autenticación OAuth y Despacho HTTPS**: Obtención de token Bearer y despacho del correo con el PDF embebido en Base64 hacia la API de Pegasus.
6. **Traslado Físico (I/O)**: Movimiento del archivo en red hacia la carpeta `Notificados` (éxito) o `ErrorNoNotificados` (fallo de negocio).
7. **Confirmación en Broker (Ack / Nack)**: Emisión de `BasicAckAsync` ante procesamiento exitoso, o `BasicNackAsync` ante fallos críticos de red.

---

### ⚠️ Problemas del Ecosistema Legado (`GeneradorCorreoSoftnet`)

El código original (`GCSLogic.cs`) presentaba un diseño procedimental, síncrono y altamente acoplado a un sistema de archivos inestable. Los principales cuellos de botella eran:

* **Polling Ineficiente y Estado Global Mutante**: El servicio no era reactivo; dependía de un temporizador que escaneaba una carpeta y usaba una variable estática (`NombreArchicoAnterior`) para evitar procesar el mismo archivo dos veces. Esto provocaba que si dos procesos enviaban un archivo con el mismo nombre, o si el servicio se reiniciaba, se perdiera la trazabilidad.
* **Dependencia Frágil de Archivos CSV (`migration-sheet.csv`)**: La metadata de envío se leía desde un archivo CSV residual usando `TextFieldParser`. La extracción estaba *hardcodeada* por índices estrictos (`fields[15]`, `fields[19]`), lo que causaba rupturas masivas ante el menor cambio en la estructura del archivo.
* **Malabares de I/O y Bloqueos de Hilo (`Thread.Sleep`)**: El sistema movía archivos entre tres directorios distintos (`Files` -> `Pub` -> `Respaldo`). Para lidiar con archivos bloqueados, recurría a capturar excepciones `IOException` e implementar pausas ciegas (`System.Threading.Thread.Sleep(2000)`), lo que congelaba el worker entero.
* **Peticiones HTTP Síncronas (RestSharp)**: La comunicación con la API de Softnet se realizaba bloqueando el hilo de ejecución mediante `client.Execute(request)`. Además, solicitaba un nuevo token OAuth en **cada iteración**, saturando el servidor de autenticación y generando latencia innecesaria.
* **Lógica de Negocio Acoplada**: Reglas complejas de seguros y validaciones de género (separando "Estimado" y "Estimada" con `string.Split()`) estaban incrustadas directamente en la clase de infraestructura, violando los principios de Clean Architecture.

### 📊 Comparativa de Arquitectura

| Dimensión | Monolito Legado (`GeneradorCorreoSoftnet`) | Nueva Arquitectura Consumer (.NET 10) |
| --- | --- | --- |
| **Mecanismo de Disparo** | Polling de carpetas (`Directory.GetFiles`) | **Event-Driven**: Cola RabbitMQ reactiva (`DocumentProcessedEvent`). |
| **Origen de Metadatos** | Archivo físico transitorio (`migration-sheet.csv`) | **Payload en Memoria**: Datos inyectados desde el Message Broker. |
| **Extracción de Datos** | VisualBasic `TextFieldParser` con índices hardcodeados | Serialización JSON tipada (`System.Text.Json`). |
| **Manejo de Archivos en Red** | `File.Copy` / `File.Delete` con `Thread.Sleep(2000)` | **Atómico**: Cliente nativo `SMBLibrary` en `NetworkScope`. |
| **Control de Concurrencia** | Variables estáticas (`NombreArchicoAnterior`) | **QoS Prefetch = 1**: Control exacto del ciclo de vida del mensaje. |
| **Peticiones HTTP** | Síncronas bloqueantes (`RestSharp v106-`) | **Asíncronas (`async/await`)** mediante `IHttpClientFactory`. |
| **Autenticación API** | Petición de token OAuth por cada archivo procesado | **Memoria Caché**: Reutilización del token hasta su expiración. |
| **Patrón Arquitectónico** | Monolito ("Clase Dios" de 400+ líneas) | **Clean Architecture** + **CQRS** + **MediatR**. |

---

## 🏛️ 2. Arquitectura del Sistema (Clean Architecture)

El proyecto aísla la lógica de negocio y consumo de los proveedores de mensajería, I/O y protocolos de red:

```text
EmailDispatchingConsumer.sln
│
├── 📂 EmailDispatchingConsumer (Worker / Host de Servicio Windows)
│   ├── HostedService.cs                # Ciclo de vida BackgroundService & Scopes por mensaje
│   ├── Program.cs                      # Host Builder, Configuración de Windows Service y DI
│   └── appsettings.json                # Configuración central (Oracle, NAS, Pegasus, RabbitMQ)
│
├── 📂 EmailDispatchingConsumer.Application (Capa de Aplicación)
│   ├── Actions/                        # Sub-Handlers CQRS (ArchiveDocument, BuildPayload, ExecuteDispatch)
│   ├── Configuration/                  # Métodos de extensión para DI (Consumer, Logging, MediatR)
│   └── UseCases/                       # Orquestadores CQRS (ProcessEmailDispatchHandler, WarmupRoutes)
│
├── 📂 EmailDispatchingConsumer.Core (Capa de Dominio y Abstracciones)
│   ├── Constants/                      # Constantes de Settings, Mensajes, Logs y Códigos de Error
│   ├── Dto/ & Models/                  # TramaEnvio, Documento, Settings fuertemente tipados
│   └── Interfaces/                     # Contratos (IRepository, IEmailDispatchingService, IStorageService, etc.)
│
└── 📂 EmailDispatchingConsumer.Infrastructure (Capa de Infraestructura)
    ├── Persistence/                    # Repositorio Dapper y conexión gestionada a Oracle DB
    └── Services/                       # SMBLibrary, RabbitMQ Channel/Consumer, Pegasus HttpClient

```

---

## 🧩 3. Patrones de Diseño, Orquestación y Consumo de Eventos

### 🔀 1. CQRS y Orquestación Atómica con MediatR

El orquestador principal (`ProcessEmailDispatchHandler`) implementa el patrón **CQRS** mediante MediatR, delegando tareas atómicas mediante acciones especializadas (*Macro Actions*):

```text
ProcessEmailDispatchCommand (Mediator)
 │
 ├──► EnsureRoutesLoadedAsync           (Validación y caché de rutas NAS en Oracle)
 ├──► RunInNetworkScope                 (Lectura de bytes del PDF en NAS)
 ├──► BuildPegasusPayloadActionRequest  (Construcción de la TramaEnvio dinámica sin CSV)
 ├──► ExecuteEmailDispatchActionRequest (Consumo HTTPS de token y notificación Pegasus)
 └──► ArchiveDocumentActionRequest      (Traslado atómico a Notificados o ErrorNoNotificados)

```

### ⚡ 2. Consumo Asíncrono Reactivo (RabbitMQ Client v7.x)

El servicio utiliza el nuevo modelo completamente asíncrono de RabbitMQ (`RabbitMQ.Client 7.x`):

* **QoS Prefetch = 1**: Garantiza que el worker procese un único documento a la vez, evitando saturar la memoria RAM.
* **Ack / Nack Diferenciado**:
* `BasicAckAsync`: El documento fue despachado exitosamente y archivado.
* `BasicNackAsync (requeue: false)`: Ocurrió un fallo técnico no controlado (caída del NAS) para derivar el mensaje a reintento o DLQ (*Dead Letter Queue*).



### 🌐 3. Integración HTTP Tipada con API Pegasus / Softnet

El servicio `EmailDispatchingService` encapsula la comunicación con la plataforma externa:

* **Flujo de Seguridad OAuth**: Solicitud de token Bearer previo al envío mediante `grant_type: password` **apoyado por caché en memoria**.
* **Detección del "Falso Positivo" (HTTP 200 con rechazo de negocio)**: Inspección del cuerpo JSON de respuesta para detectar casos donde la API externa responde código HTTP `200 OK`, pero con la propiedad `estado: false`, garantizando el ruteo a la carpeta de error.

### 🔄 4. Aislamiento Scoped en BackgroundService

Para garantizar la liberación determinista de memoria y conexiones, el `HostedService` genera un `IServiceScope` independiente por cada mensaje consumido de la cola:

```csharp
await eventConsumerService.StartConsumingAsync<DocumentProcessedEvent>(
    async (documentEvent, ct) =>
    {
        using IServiceScope scope = serviceScopeFactory.CreateScope();
        ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();

        return await sender.Send(new ProcessEmailDispatchCommand(documentEvent), ct);
    },
    stoppingToken
);

```

---

## 🛠️ 4. Stack Tecnológico

| Categoría | Tecnología / Librería | Versión | Propósito |
| --- | --- | --- | --- |
| ⚡ **Runtime** | **.NET 10.0** | `10.0` | Framework base (`net10.0`) con C# 14, `ImplicitUsings` y `Nullable`. |
| 🖥️ **Host / Service** | **Microsoft.Extensions.Hosting.WindowsServices** | `10.0.12` | Integración nativa del Worker para ejecutarse como Servicio de Windows. |
| 🔀 **Arquitectura** | **MediatR** | `12.5.0` | Implementación del patrón Mediator, CQRS y pipeline de acciones. |
| 🐇 **Mensajería** | **RabbitMQ.Client** | `7.2.2` | Consumo asíncrono reactivo (`BasicQosAsync`, `BasicAckAsync`, `BasicNackAsync`). |
| 🌐 **HTTP Client** | **Microsoft.Extensions.Http** | `10.0.x` | Cliente HTTP tipado y resiliente (reemplazo de RestSharp). |
| 🔄 **JSON Engine** | **System.Text.Json** | Nativo | Parseo de alta velocidad en memoria (`JsonDocument`). |
| 📁 **Almacenamiento Red** | **SMBLibrary** | `1.5.8.1` | Cliente nativo SMB1/SMB2/SMB3 para interacción transaccional con NAS. |
| 🗄️ **Base de Datos** | **Oracle.ManagedDataAccess.Core** | `23.26.301` | Driver oficial gestionado para conexión con Oracle DB. |
| 🚀 **ORM** | **Dapper** | `2.1.89` | Consultas de alto rendimiento para carga de parámetros de rutas. |
| 📝 **Logging** | **Serilog.AspNetCore** | `10.0.0` | Logging estructurado hacia consola y archivos físicos rotativos. |

---

## ⚙️ 5. Desglose Detallado de Macro Actions y Use Cases (`Actions/` & `UseCases/`)

Los componentes CQRS modulares encargados de resolver tareas específicas de negocio son:

* 🚀 **`ProcessEmailDispatchHandler`**: Orquestador central del caso de uso. Lee y valida rutas, coordina la lectura binaria en red, ensambla la trama, dispara el envío HTTP y traslada los archivos al destino final.
* 🔄 **`WarmupConsumerRoutesCommand`**: Comando de precarga ejecutado en el arranque del servicio para asegurar la disponibilidad de rutas NAS antes de recibir eventos.
* 📦 **`BuildPegasusPayloadActionRequest`**: Sub-handler encargado de construir el objeto `TramaEnvio` sin depender de archivos CSV.
* ✉️ **`ExecuteEmailDispatchActionRequest`**: Sub-handler responsable de la comunicación externa (gestión del token OAuth con caché, conversión del PDF a Base64 y POST del correo).
* 🚚 **`ArchiveDocumentActionRequest`**: Sub-handler de I/O en red encargado de mover físicamente el documento hacia `Notificados` o `ErrorNoNotificados`.

---

## 🔧 6. Configuración del Sistema (`appsettings.json`)

```json
{
  "ConnectionStrings": {
    "Database": "Data Source=<DB_HOST>:<DB_PORT>/<SERVICE_NAME>;User Id=<DB_USER>;Password=<DB_PASSWORD>;"
  },
  "ConsumerSettings": {
    "LogDirectoryPath": "C:\\Logs\\EmailDispatchingConsumer\\consumer-.log"
  },
  "NASSettings": {
    "Host": "Prtsrvnasapp",
    "Domain": "PROTECTA",
    "Username": "<NAS_USER>",
    "Password": "<NAS_PASSWORD>",
    "ShareFolder": "PRD"
  },
  "ApiPegasusSettings": {
    "NotificacionUrl": "https://intermediacion.pegasus.com.pe/intermediacion-api/notificacion/registrarNotificacion",
    "TokenUrl": "https://intermediacion.pegasus.com.pe/intermediacion-api/oauth/token",
    "AuthHeader": "Basic <BASE64_AUTH>",
    "Username": "<PEGASUS_USER>",
    "Password": "<PEGASUS_PASSWORD>"
  },
  "RabbitMQSettings": {
    "Host": "<RABBITMQ_HOST>",
    "Port": 5672,
    "VirtualHost": "/",
    "Username": "DigitalSigner",
    "Password": "<RABBITMQ_PASSWORD>",
    "ExchangeName": "e-policy.exchange",
    "RoutingKey": "e-policy.signed",
    "QueueName": "e-policy.signed.queue"
  }
}

```

---

## ⚙️ 7. Pipeline y Extensibilidad (`Program.cs`)

El punto de entrada `Program.cs` inicializa el host con soporte para Servicios de Windows y configura los módulos del sistema:

```csharp
var builder = Host.CreateApplicationBuilder(args);

// Integración con el Gestor de Servicios de Windows
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = SettingsConstants.WINDOWS_SERVICE_NAME;
});

// Extensiones de builder (Servicios & DI)
builder.AddLogs();        // Configuración de Serilog estructurado
builder.AddConsumer();    // Configuración de SMBLibrary, HttpClient tipado, RabbitMQ y MediatR
builder.AddDataBase();    // Registro de persistencia Oracle y repositorios Dapper

// HostedService principal del ciclo de vida
builder.Services.AddHostedService<HostedService>();

var host = builder.Build();
await host.RunAsync();

```

---

## 🗄️ 8. Configuración de Base de Datos y Rutas Parametrizadas

El consumidor obtiene dinámicamente las rutas de carpetas de almacenamiento NAS desde Oracle DB. Parámetros clave obtenidos desde BD:

* `DB_PARAM_FIRMADOS`: Directorio de entrada donde reposan los PDFs firmados por `DigitalSigningTaskRunner`.
* `DB_PARAM_NOTIFICADOS`: Directorio de destino final para documentos notificados exitosamente.
* `DB_PARAM_ERROR_NO_NOTIFICADOS`: Directorio de contingencia para documentos cuyo despacho o validación fue rechazado.

---

## 📦 9. Instalación y Despliegue como Servicio de Windows

Para compilar y registrar el worker como un Servicio de Windows en entornos de producción:

1. **Compilar y Publicar la Solución**:

```bash
dotnet publish EmailDispatchingConsumer/EmailDispatchingConsumer.csproj -c Release -o C:\Services\EmailDispatchingConsumer

```

2. **Crear el Servicio de Windows con `sc.exe**`:

```cmd
sc.exe create "E-policy - Email Dispatching Consumer" binPath= "C:\Services\EmailDispatchingConsumer\EmailDispatchingConsumer.exe" start= auto

```

3. **Iniciar el Servicio**:

```cmd
net start "E-policy - Email Dispatching Consumer"

```
