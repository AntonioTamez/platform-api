# Guía: Revisar Manualmente tus Recursos GCP en la Consola

> Cómo navegar la consola de Google Cloud para verificar cada cosa que creaste,
> revisar que el servicio esté sano, y monitorear los costos en tiempo real.

Abre la consola en: `https://console.cloud.google.com`

Asegúrate de que en la parte superior esté seleccionado **tu proyecto** (menú desplegable al lado del logo de Google Cloud).

---

## 1. Ver el servicio de Cloud Run

### Cómo llegar

1. Menú hamburguesa (☰) → **"Cloud Run"**
2. O busca "Cloud Run" en la barra de búsqueda superior

### Qué deberías ver

Una tabla con tu servicio:

| Service | Region | Last deployed | Status |
|---------|--------|---------------|--------|
| persons-api | us-central1 | hace X minutos | ● (verde) |

El punto verde significa que el servicio está activo. Si es rojo, hay un problema.

### Ver los detalles del servicio

Haz clic en **"persons-api"**. Verás:

**Pestaña "METRICS"**
- **Request count:** cuántas peticiones ha recibido
- **Request latency:** cuánto tarda en responder
- **Container instance count:** cuántos contenedores están corriendo (0 = en reposo, costo $0)
- **Startup latency:** tiempo de cold start cuando escala desde 0

**Pestaña "REVISIONS"**
- Lista de cada deploy que has hecho
- La revisión activa tiene "100%" de tráfico
- Estado "Ready ✓" = sin crash loop

**Pestaña "LOGS"**
- Acceso directo a los logs de este servicio (más fácil que ir a Cloud Logging directamente)

**Pestaña "YAML"**
- La configuración completa del servicio en formato YAML — útil para verificar que `--port 8080`, `--memory 512Mi`, etc. quedaron correctos

---

## 2. Ver las imágenes en Artifact Registry

### Cómo llegar

1. Menú hamburguesa (☰) → **"Artifact Registry"** → **"Repositories"**

### Qué deberías ver

| Name | Format | Location | Size |
|------|--------|----------|------|
| personsapi | Docker | us-central1 | ~200-350 MB |

### Ver las imágenes dentro del repositorio

1. Haz clic en **"personsapi"**
2. Verás la imagen `personsapi` con el tag `latest`
3. Haz clic en la imagen para ver:
   - El digest SHA256 exacto
   - Cuándo fue pusheada
   - Los layers que la componen
   - Vulnerabilidades detectadas (si tienes Container Scanning habilitado)

---

## 3. Ver la Service Account y sus permisos

### Cómo llegar

1. Menú hamburguesa (☰) → **"IAM & Admin"** → **"Service Accounts"**

### Qué deberías ver

| Name | Email | Keys |
|------|-------|------|
| PersonsAPI Deployer | persons-api-deployer@TU-PROJECT-ID.iam.gserviceaccount.com | 1 key |

El "1 key" es el `key.json` que descargaste localmente.

### Ver los roles (permisos)

1. Menú hamburguesa (☰) → **"IAM & Admin"** → **"IAM"**
2. Busca `persons-api-deployer` en la lista
3. Deberías ver exactamente 2 roles:
   - `Artifact Registry Writer`
   - `Cloud Run Admin`

Si ves `Owner` o `Editor` ahí, eso sería un error de seguridad (over-permission). En tu caso solo deben estar los 2 roles de arriba.

---

## 4. Ver los Logs en Cloud Logging

### Cómo llegar

**Opción A — Desde Cloud Run (más rápido):**
1. Ve a Cloud Run → persons-api → pestaña **"LOGS"**

**Opción B — Desde Cloud Logging:**
1. Menú hamburguera (☰) → **"Logging"** → **"Logs Explorer"**
2. En el campo de consulta escribe:
   ```
   resource.type="cloud_run_revision" AND resource.labels.service_name="persons-api"
   ```
3. Presiona **"Run Query"**

### Qué deberías ver

Entradas JSON de Serilog. Cada entrada tiene estructura como:
```json
{
  "@t": "2026-06-04T...",
  "@m": "Request finished HTTP/1.1 GET /health - 200 ...",
  "@l": "Information",
  "RequestPath": "/health",
  "StatusCode": 200,
  ...
}
```

### Filtros útiles

Solo errores:
```
resource.type="cloud_run_revision"
AND resource.labels.service_name="persons-api"
AND severity>=ERROR
```

Solo peticiones al health endpoint:
```
resource.type="cloud_run_revision"
AND resource.labels.service_name="persons-api"
AND jsonPayload.RequestPath="/health"
```

Solo las últimas 2 horas (usa el selector de tiempo en la parte superior derecha).

---

## 5. Monitorear los costos

### Cómo llegar

1. Menú hamburguesa (☰) → **"Billing"**
2. Haz clic en tu cuenta de facturación
3. En el menú izquierdo: **"Reports"**

### Qué deberías ver

**Vista principal — Reports:**
- Gráfica de costos por día
- Para este proyecto, debería estar en $0.00 o centavos
- Filtra por proyecto si tienes varios

**Ver crédito restante:**
1. En el menú izquierdo: **"Credits"**
2. Verás tus $300 USD de crédito gratuito y cuánto has usado

**Ver el desglose por servicio:**
1. En "Reports", cambia el grupo en "Group by" → **"Service"**
2. Verás el costo separado por: Cloud Run, Artifact Registry, etc.

### Alertas de presupuesto (recomendado)

Para que nunca llegue una sorpresa en la factura:

1. En Billing → **"Budgets & alerts"**
2. Haz clic en **"Create budget"**
3. Configura:
   - **Scope:** tu proyecto
   - **Amount:** $5 USD (o lo que quieras como límite)
   - **Alerts:** 50%, 90%, 100%
   - **Email notifications:** tu correo
4. Guarda

Cuando gastes el 50% de $5 (o sea $2.50), recibirás un email. Esto no detiene el servicio, solo te avisa.

---

## 6. Ver el historial de deploys

### Cómo llegar

1. Cloud Run → **"persons-api"** → pestaña **"REVISIONS"**

Cada vez que corras `gcloud run deploy` se crea una nueva revisión. Puedes:
- Ver cuál está activa (tiene el 100% del tráfico)
- Hacer rollback a una revisión anterior haciendo clic en los 3 puntos (⋮) → "Manage Traffic"
- Ver los logs específicos de esa revisión

---

## 7. Ver el estado de las APIs habilitadas

### Cómo llegar

1. Menú hamburguesa (☰) → **"APIs & Services"** → **"Enabled APIs & services"**

Deberías ver (entre otras):
- **Cloud Run Admin API** — habilitada
- **Artifact Registry API** — habilitada

Si algún día necesitas habilitar otra API (ej. Secret Manager para la Phase 8), es desde aquí donde puedes buscarla y habilitarla visualmente en lugar de usar el comando `gcloud services enable`.

---

## 8. Verificar el servicio está vivo desde la consola

Desde Cloud Run → persons-api, haz clic en la URL del servicio (arriba del todo, junto al nombre). Se abre en el navegador.

Prueba estas rutas directamente en el navegador:
- `https://persons-api-xxxx-uc.a.run.app/health` → debe mostrar `{"status":"Healthy"}`
- `https://persons-api-xxxx-uc.a.run.app/api/persons` → debe mostrar el array de 3 personas

---

## Resumen: mapa de la consola

| ¿Qué quiero ver? | Dónde ir en la consola |
|-----------------|----------------------|
| Estado del servicio | Cloud Run → persons-api |
| Métricas (tráfico, latencia) | Cloud Run → persons-api → METRICS |
| Logs de la app | Cloud Run → persons-api → LOGS |
| Imágenes Docker | Artifact Registry → personsapi |
| Permisos de la service account | IAM & Admin → IAM |
| Claves de la service account | IAM & Admin → Service Accounts |
| Costos en tiempo real | Billing → Reports |
| Crédito gratuito restante | Billing → Credits |
| Alertas de presupuesto | Billing → Budgets & alerts |
| APIs habilitadas | APIs & Services → Enabled APIs |

---

*PersonsAPI v2.0 — Guía de revisión manual GCP*
