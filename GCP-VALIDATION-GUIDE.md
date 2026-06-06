# GCP Console — Guía de Validación

Documento de referencia para verificar visualmente en GCP Console todo lo que se configuró y desplegó en este proyecto. Cubre Artifact Registry, Cloud Run, IAM, Logging, y Billing.

**Proyecto:** PersonsAPI  
**Región:** `us-central1` (Iowa)  
**Servicio Cloud Run:** `persons-api`  
**Repositorio Artifact Registry:** `personsapi`

---

## Índice

1. [APIs habilitadas](#1-apis-habilitadas)
2. [Artifact Registry — imagen Docker](#2-artifact-registry--imagen-docker)
3. [Cloud Run — servicio en producción](#3-cloud-run--servicio-en-producción)
4. [Cloud Logging — logs del API](#4-cloud-logging--logs-del-api)
5. [IAM — cuentas de servicio y permisos](#5-iam--cuentas-de-servicio-y-permisos)
6. [Billing — revisar costos](#6-billing--revisar-costos)

---

## 1. APIs habilitadas

**Ruta:** Menú (≡) → **APIs & Services** → **Enabled APIs & services**

Verifica que estas tres APIs estén en la lista:

| API | Para qué se usa |
|-----|-----------------|
| **Cloud Run API** | Desplegar y ejecutar el servicio `persons-api` |
| **Artifact Registry API** | Almacenar la imagen Docker |
| **Cloud Build API** | Habilitada como dependencia de Artifact Registry |

**Cómo verificar:** En el buscador de la página escribe el nombre de cada API y confirma que aparece en la lista de habilitadas (no en "Available to enable").

---

## 2. Artifact Registry — imagen Docker

**Ruta:** Menú (≡) → **Artifact Registry** → **Repositories**

### 2.1 El repositorio

Deberías ver un repositorio llamado `personsapi`:

| Campo | Valor esperado |
|-------|---------------|
| **Name** | `personsapi` |
| **Format** | Docker |
| **Location** | `us-central1` |
| **Mode** | Standard |

Click en **`personsapi`** para entrar.

### 2.2 La imagen

Dentro del repositorio verás una carpeta/imagen llamada `personsapi`. Click en ella.

| Campo | Valor esperado |
|-------|---------------|
| **Image name** | `personsapi` |
| **Tags** | `latest` |
| **Digest** | `sha256:...` (cambia con cada push) |
| **Upload time** | La fecha y hora del último push |
| **Size** | ~200-300 MB (imagen multi-stage .NET 10) |

**Qué confirma esto:** La imagen que corre en Cloud Run es exactamente esta. Cada push del CI/CD sobreescribe el tag `:latest` con una nueva imagen.

### 2.3 Detalles de la imagen

Click en el **digest** (`sha256:...`) para ver el detalle:

- **Layers:** Verás las capas del Dockerfile multi-stage (base `aspnet:10.0` + capas de la app)
- **OS/Architecture:** `linux/amd64` — compatible con los runners de GitHub Actions y Cloud Run
- **Created:** Timestamp del `docker build`

### 2.4 Permisos del repositorio

Dentro del repositorio, busca la pestaña o botón **Permissions** / **Show info panel**:

- Deberías ver a `persons-api-deployer@PROJECT_ID.iam.gserviceaccount.com` con el rol **Artifact Registry Writer**

---

## 3. Cloud Run — servicio en producción

**Ruta:** Menú (≡) → **Cloud Run**

### 3.1 El servicio

Verás el servicio `persons-api` en la lista:

| Campo | Valor esperado |
|-------|---------------|
| **Service** | `persons-api` |
| **Region** | `us-central1` |
| **URL** | `https://persons-api-HASH-uc.a.run.app` |
| **Status** | ✓ (check verde) |
| **Last deployed** | Fecha del último deploy |

Click en **`persons-api`** para ver el detalle.

### 3.2 URL pública

En la parte superior de la página verás la URL del servicio. Puedes:

1. Copiar la URL y pegarla en el navegador
2. Ir a `https://persons-api-HASH-uc.a.run.app/health` → debe responder `{"status":"Healthy"}`
3. Ir a `https://persons-api-HASH-uc.a.run.app/api/persons` → debe responder con 3 personas en JSON
4. Ir a `https://persons-api-HASH-uc.a.run.app/scalar` → debe abrir el UI de Scalar (documentación interactiva)

### 3.3 Revisiones (Revisions)

Pestaña **Revisions**:

- Cada deploy crea una nueva revisión
- La revisión activa tiene el 100% del tráfico
- Verás el historial de todos los deploys anteriores
- El tag de la imagen en cada revisión debe ser `us-central1-docker.pkg.dev/PROJECT_ID/personsapi/personsapi:latest`

### 3.4 Configuración del servicio

Pestaña **Edit & Deploy New Revision** (solo para leer — no guardes cambios):

| Parámetro | Valor configurado | Por qué |
|-----------|------------------|---------|
| **Container image** | `...personsapi:latest` | Imagen del Artifact Registry |
| **Container port** | `8080` | `ASPNETCORE_HTTP_PORTS=8080` en Dockerfile |
| **Memory** | `512 MiB` | Headroom para .NET 10 + EF InMemory (~180-250 MiB baseline) |
| **CPU** | `1` | Estándar para API de aprendizaje |
| **Minimum instances** | `0` | Scale-to-zero — costo $0 en reposo |
| **Maximum instances** | (default Cloud Run) | Sin límite manual |
| **Request timeout** | (default 300s) | No modificado |
| **Concurrency** | (default 80) | No modificado |

**Variables de entorno:** Ninguna definida — el `ASPNETCORE_HTTP_PORTS=8080` viene del Dockerfile directamente.

### 3.5 Autenticación

En la pestaña **Security** o en la vista general:

- **Authentication:** `Allow unauthenticated invocations` — público, cualquiera puede llamar la API sin token IAM. Correcto para un proyecto de aprendizaje.

### 3.6 Métricas

Pestaña **Metrics**:

| Métrica | Qué muestra |
|---------|-------------|
| **Request count** | Número de requests recibidos |
| **Request latency** | P50/P95/P99 de latencia |
| **Container instance count** | Cuántas instancias están activas (0 cuando no hay tráfico) |
| **CPU utilization** | Uso de CPU por instancia |
| **Memory utilization** | Uso de memoria (debería estar ~200-250 MiB) |

**Cold start:** Si el servicio lleva tiempo sin tráfico, la primera request tardará ~3-5 segundos extra mientras Cloud Run levanta el contenedor desde 0 instancias.

---

## 4. Cloud Logging — logs del API

**Ruta:** Menú (≡) → **Logging** → **Logs Explorer**

### 4.1 Ver logs del servicio

En el query builder escribe:

```
resource.type="cloud_run_revision"
resource.labels.service_name="persons-api"
resource.labels.location="us-central1"
```

Click **Run query**.

### 4.2 Qué deberías ver

Cada log entry es una línea JSON en formato CLEF (Compact Log Event Format) producida por Serilog. Ejemplo de una entrada:

```json
{
  "@t": "2026-06-05T10:23:45.1234567Z",
  "@mt": "Now listening on: {address}",
  "address": "http://[::]:8080",
  "SourceContext": "Microsoft.Hosting.Lifetime"
}
```

Tipos de logs que verás:

| Log | Cuándo aparece |
|-----|---------------|
| `Application started` | Al iniciar el contenedor (cold start) |
| `Now listening on: http://[::]:8080` | Cuando Kestrel levanta |
| `HTTP GET /health 200` | Cada llamada al health check |
| `HTTP GET /api/persons 200` | Cada llamada al endpoint de personas |
| `HTTP POST /api/persons 201` | Cada persona creada |
| `EF Core` queries | Filtradas a `Warning` (no deberían aparecer en condiciones normales) |

### 4.3 Filtrar por severidad

En el dropdown de severidad selecciona:
- **Info** — logs normales de operación
- **Warning** — alertas (EF Core, ASP.NET Core framework)
- **Error** — errores de la aplicación (no debería haber en condiciones normales)

### 4.4 Ver logs de un deploy específico de CI/CD

Cuando el pipeline de GitHub Actions despliega, puedes correlacionar los logs con el timestamp del deploy viendo en Cloud Run → Revisions la hora de la revisión y filtrando en Logging por ese rango de tiempo.

---

## 5. IAM — cuentas de servicio y permisos

### 5.1 Ver todas las cuentas de servicio

**Ruta:** Menú (≡) → **IAM & Admin** → **Service Accounts**

Deberías ver (al menos) estas dos cuentas:

| Email | Nombre | Para qué |
|-------|--------|----------|
| `persons-api-deployer@PROJECT_ID.iam.gserviceaccount.com` | persons-api-deployer | CI/CD: push imagen + deploy Cloud Run |
| `PROJECT_NUMBER-compute@developer.gserviceaccount.com` | Compute Engine default service account | Runtime: lo que usa el contenedor en Cloud Run |

### 5.2 Verificar roles de persons-api-deployer

Click en `persons-api-deployer@...`:

- Pestaña **Details:** muestra el nombre, email, y descripción
- Pestaña **Permissions:** muestra los roles asignados a nivel de proyecto

**Ruta alternativa más clara:** Menú (≡) → **IAM & Admin** → **IAM** → buscar `persons-api-deployer` en la tabla.

Debe tener estos dos roles:

| Rol | ID del rol | Para qué |
|-----|------------|----------|
| **Artifact Registry Writer** | `roles/artifactregistry.writer` | Push de imágenes Docker al repositorio `personsapi` |
| **Cloud Run Admin** | `roles/run.admin` | Desplegar y gestionar el servicio `persons-api` |

### 5.3 Verificar el permiso actAs (iam.serviceAccountUser)

Este es el permiso que se agregó para resolver el error de deploy.

**Ruta:** Menú (≡) → **IAM & Admin** → **Service Accounts** → click en `PROJECT_NUMBER-compute@developer.gserviceaccount.com` → pestaña **Permissions**

Deberías ver:

| Principal | Rol |
|-----------|-----|
| `persons-api-deployer@PROJECT_ID.iam.gserviceaccount.com` | Service Account User |

**Por qué existe este permiso:** Cuando Cloud Run despliega una nueva revisión, necesita asignar un runtime service account al contenedor. Por defecto usa el Compute Engine default SA. Para poder "actuar como" ese SA durante el deploy, `persons-api-deployer` necesita `iam.serviceaccounts.actAs` sobre él — que es exactamente lo que otorga el rol **Service Account User**.

### 5.4 Ver la clave JSON (sin revelar su contenido)

**Ruta:** Menú (≡) → **IAM & Admin** → **Service Accounts** → click en `persons-api-deployer@...` → pestaña **Keys**

Deberías ver la clave con:
- **Key ID:** un hash largo (el mismo que está en `key.json` como `"private_key_id"`)
- **Type:** JSON
- **Created:** fecha de creación (cuando la descargaste en Phase 7)
- **Status:** Active

> ⚠️ **Nunca** hagas click en "Download" de nuevo ni compartas el Key ID públicamente. El archivo `key.json` en tu máquina local es el mismo que está en el secret `GCP_SA_KEY` de GitHub Actions.

---

## 6. Billing — revisar costos

### 6.1 Ver el costo total del proyecto

**Ruta:** Menú (≡) → **Billing** → **Reports**

Selecciona:
- **Time range:** Últimos 30 días (o el período que quieras)
- **Group by:** Service

### 6.2 Servicios que generan costo

| Servicio | Costo esperado | Detalle |
|----------|---------------|---------|
| **Cloud Run** | ~$0 en reposo | Factura por requests y CPU activa. Con `min-instances=0` el costo es $0 cuando no hay tráfico. Primeros 2M requests/mes son gratis (free tier). |
| **Artifact Registry** | < $0.10/mes | Factura por GB almacenado (~$0.10/GB/mes). Una imagen .NET de ~250 MB cuesta ~$0.025/mes. |
| **Networking** | ~$0 | El egress dentro de la misma región es gratuito. Egress externo: primeros 100 GB/mes son gratis. |

**En total:** Para un proyecto de aprendizaje con tráfico mínimo, el costo mensual debería ser **$0 o menos de $0.10**.

### 6.3 Ver el detalle por servicio

En **Reports**, haz click en **Cloud Run** en la tabla para ver el desglose:
- **CPU allocation time:** tiempo que la instancia estuvo activa procesando requests
- **Memory allocation time:** tiempo que la memoria estuvo asignada
- **Request count:** número de requests facturados

### 6.4 Configurar alertas de costo (recomendado)

**Ruta:** Billing → **Budgets & alerts** → **Create budget**

Configura un presupuesto de $5/mes con alerta al 50% y 100% para que te avise por email si algo sube inesperadamente.

### 6.5 Costo de GitHub Actions

Los minutos de GitHub Actions para repos **públicos son gratuitos e ilimitados**. Para repos privados, hay 2,000 minutos/mes en el plan gratuito. Cada run del pipeline toma ~3-5 minutos.

---

## Resumen rápido de validación

Checklist para confirmar que todo funciona correctamente:

| Qué verificar | Dónde | Resultado esperado |
|---------------|-------|--------------------|
| ✅ Imagen Docker presente | Artifact Registry → personsapi → personsapi | Tag `:latest` visible con fecha del último push |
| ✅ Servicio corriendo | Cloud Run → persons-api | Status ✓ verde, URL activa |
| ✅ API responde | `URL/health` en el navegador | `{"status":"Healthy"}` |
| ✅ Datos disponibles | `URL/api/persons` en el navegador | JSON con 3 personas |
| ✅ Logs en JSON | Cloud Logging → query persons-api | Líneas CLEF con `@t` y `@mt` |
| ✅ Roles del deployer | IAM → persons-api-deployer | artifactregistry.writer + run.admin |
| ✅ Permiso actAs | Service Accounts → Compute default SA → Permissions | persons-api-deployer con Service Account User |
| ✅ Clave activa | Service Accounts → persons-api-deployer → Keys | 1 clave JSON Active |
| ✅ APIs habilitadas | APIs & Services → Enabled | Cloud Run, Artifact Registry, Cloud Build |
| ✅ Costo razonable | Billing → Reports | < $0.10/mes |

---

*Generado: 2026-06-05 — PersonsAPI v2.0 Cloud Deployment*
