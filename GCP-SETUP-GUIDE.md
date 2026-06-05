# Guía Completa: Desplegar PersonsAPI en Google Cloud Run

> Guía paso a paso para alguien que nunca ha usado GCP. En español, extremadamente detallada.

---

## Antes de empezar — entiende estas 3 cosas

**1. ¿Qué es un Project ID?**
En GCP, todo vive dentro de un "proyecto". El Project ID es un nombre único global (como un dominio de internet — si alguien ya lo usó, tú no puedes). Tienes que elegirlo tú. Ejemplo: `personsapi-2026` o `miprimergcp-dev`. Reglas:
- Solo letras minúsculas, números y guiones
- Entre 6 y 30 caracteres
- Debe empezar con una letra
- Una vez creado, **no se puede cambiar**

**2. ¿Necesito tarjeta de crédito?**
Sí. GCP requiere tarjeta para activar la cuenta, pero te da **$300 USD de crédito gratuito**. Este proyecto cuesta centavos, no dólares. No se te cobrará nada si haces la limpieza del Apéndice al final.

**3. ¿Desde dónde ejecuto los comandos?**
Todo desde PowerShell en la raíz de tu proyecto: `C:\ATS\Git\platform`. Abre PowerShell y navega ahí primero:

```powershell
cd C:\ATS\Git\platform
```

---

## FASE 0: Instalar el gcloud CLI

> **¿Ya lo tienes?** Verifica en PowerShell: `gcloud version`. Si imprime una versión (ej. `Google Cloud SDK 500.x.x`), salta directo a la Fase 1.

### 0.1 Descarga el instalador

Abre tu navegador y entra a esta URL exacta:
```
https://dl.google.com/dl/cloudsdk/channels/rapid/GoogleCloudSDKInstaller.exe
```
Se descargará un archivo `.exe` de ~50 MB.

### 0.2 Ejecuta el instalador

1. Doble clic en `GoogleCloudSDKInstaller.exe`
2. Acepta los términos
3. Selecciona "Install for all users" (recomendado) o "Install for current user"
4. Deja todas las opciones marcadas por defecto — incluyendo "Add gcloud to PATH"
5. Haz clic en "Install" y espera (~2 minutos)
6. Al final, dejará marcada la opción **"Start Google Cloud CLI Shell"** — **desmárcala**. Usarás tu PowerShell normal.
7. Haz clic en "Finish"

### 0.3 Abre un PowerShell NUEVO

Cierra cualquier PowerShell que tengas abierto. Abre uno nuevo (el instalador modifica el PATH y los terminales viejos no lo detectan).

### 0.4 Verifica la instalación

```powershell
gcloud version
```

Debes ver algo como:
```
Google Cloud SDK 502.0.0
bq 2.1.12
core 2025.05.16
gcloud-crc32c 1.0.0
gsutil 5.34
```

Si dice `gcloud: command not found` o `gcloud no se reconoce`, cierra PowerShell y ábrelo de nuevo.

---

## FASE 1: Autenticarte con tu cuenta Google

### 1.1 Inicializa gcloud

```powershell
gcloud init
```

**¿Qué pasa?** Se abre tu navegador automáticamente pidiendo que inicies sesión con Google. Sigue estos pasos en PowerShell cuando te pregunte:

- `You must log in to continue. Would you like to log in (Y/n)?` → escribe `Y` y presiona Enter
- El navegador abre Google Sign-In → inicia sesión con tu cuenta Google
- El navegador muestra "You are now authenticated with the Google Cloud CLI!" → vuelve a PowerShell
- `Pick cloud project to use:` → escribe `1` para "Create a new project" (lo crearemos en el Paso 2)
- Si pregunta por región default, escribe `n` por ahora

### 1.2 Verifica que estás autenticado

```powershell
gcloud auth list
```

Debe mostrar tu email con un asterisco (`*`) indicando que es la cuenta activa:
```
   Credentialed Accounts
ACTIVE  ACCOUNT
*       tu-email@gmail.com
```

---

## FASE 2: Crear el proyecto GCP

### 2.1 Elige tu Project ID

**Antes de escribir el comando**, decide tu Project ID. Ejemplos válidos:
- `personsapi-2026`
- `antonio-personsapi`
- `ats-cloud-demo`

Lo usarás en **todos** los comandos que siguen. En esta guía lo llamaré `TU-PROJECT-ID` — reemplázalo por el que elegiste.

### 2.2 Crea el proyecto

```powershell
gcloud projects create TU-PROJECT-ID --name="PersonsAPI"
```

Ejemplo real:
```powershell
gcloud projects create personsapi-2026 --name="PersonsAPI"
```

**Si dice** `Project ID 'personsapi-2026' is not available` → ese nombre ya existe globalmente, elige otro.

**Si dice** `Create in progress for [https://cloudresourcemanager.googleapis.com/v1/projects/...] ... done.` → éxito.

### 2.3 Configura ese proyecto como activo

```powershell
gcloud config set project TU-PROJECT-ID
```

Verifica:
```powershell
gcloud config get-value project
```
Debe imprimir tu Project ID.

---

## FASE 3: Crear y vincular la cuenta de facturación (OBLIGATORIO antes del siguiente paso)

> **Esta fase se hace en el navegador, no en la terminal.**

### 3.1 Ve a la consola GCP

Abre: `https://console.cloud.google.com`

Inicia sesión si te lo pide.

### 3.2 Activa el período de prueba gratuito (si es tu primera vez)

Si aparece un banner que dice "Start your free trial" o "Activate free trial":
1. Haz clic en él
2. Te pedirá datos del país y tarjeta de crédito
3. Ingresa tu tarjeta — **no se hace ningún cargo** hasta que gastes los $300 de crédito gratuito y actives explícitamente la cuenta de pago
4. GCP activa los $300 USD de crédito

### 3.3 Vincula la facturación a tu proyecto

1. En la consola, haz clic en el menú hamburguesa (☰) arriba a la izquierda
2. Ve a **"Billing"** (Facturación)
3. Haz clic en **"Manage billing accounts"**
4. Verás tu cuenta de facturación — anota su ID, tiene el formato `XXXXXX-XXXXXX-XXXXXX` (ej. `01AB2C-3DE456-7F890G`)

Ahora en PowerShell, lista tus cuentas de facturación:

```powershell
gcloud billing accounts list
```

Verás algo como:
```
ACCOUNT_ID            NAME                OPEN  MASTER_ACCOUNT_ID
01AB2C-3DE456-7F890G  My Billing Account  True
```

Copia ese `ACCOUNT_ID` y vincúlalo a tu proyecto:

```powershell
gcloud billing projects link TU-PROJECT-ID --billing-account=01AB2C-3DE456-7F890G
```

(Reemplaza `01AB2C-3DE456-7F890G` con tu ACCOUNT_ID real)

**Éxito** cuando dice:
```
billingAccountName: billingAccounts/01AB2C-3DE456-7F890G
billingEnabled: true
```

---

## FASE 4: Habilitar las APIs de GCP

### 4.1 Habilita Cloud Run y Artifact Registry

```powershell
gcloud services enable run.googleapis.com artifactregistry.googleapis.com
```

**Este comando tarda 1-2 minutos.** Verás:
```
Operation "operations/acf.p2-..." finished successfully.
```

### 4.2 Verifica que quedaron habilitadas

```powershell
gcloud services list --enabled --filter="name:(run.googleapis.com OR artifactregistry.googleapis.com)"
```

Debes ver exactamente 2 entradas, ambas con `STATE: ENABLED`:
```
NAME                              TITLE
artifactregistry.googleapis.com   Artifact Registry API
run.googleapis.com                Cloud Run Admin API
```

---

## FASE 5: Crear el repositorio de imágenes Docker (Artifact Registry)

### 5.1 Crea el repositorio

```powershell
gcloud artifacts repositories create personsapi `
  --repository-format=docker `
  --location=us-central1 `
  --description="PersonsAPI Docker images"
```

> **Nota sobre el backtick (`)**: en PowerShell, el backtick es el carácter de continuación de línea (equivale al `\` en bash). Pega el comando completo tal cual — los backticks al final de cada línea unen todo en un solo comando.

**Éxito** cuando dice:
```
Created repository [personsapi].
```

### 5.2 Verifica que existe

```powershell
gcloud artifacts repositories list --location=us-central1
```

Debes ver:
```
REPOSITORY  FORMAT  MODE                 DESCRIPTION               LOCATION
personsapi  DOCKER  STANDARD_REPOSITORY  PersonsAPI Docker images  us-central1
```

---

## FASE 6: Configurar Docker para autenticarse con GCP

### 6.1 Asegúrate de que Docker Desktop está corriendo

Busca el ícono de Docker en la barra de tareas (abajo a la derecha en Windows). Debe mostrar que está corriendo. Si no está abierto, ábrelo y espera a que termine de iniciar (~30 segundos).

Verifica en PowerShell:
```powershell
docker version
```

Debe mostrar `Client:` y `Server:` con versiones. Si dice "error during connect" o similar, Docker Desktop no está corriendo.

### 6.2 Autoriza Docker con Artifact Registry

```powershell
gcloud auth configure-docker us-central1-docker.pkg.dev
```

Te pregunta:
```
Do you want to continue (Y/n)?
```
Escribe `Y` y Enter.

**Éxito** cuando dice:
```
Adding credentials for: us-central1-docker.pkg.dev
Docker configuration file updated.
```

---

## FASE 7: Crear la Service Account (cuenta de servicio)

Esta cuenta tiene permisos mínimos para hacer el deploy. También la reutilizarás en la Fase 8 (CI/CD).

### 7.1 Crea la service account

```powershell
gcloud iam service-accounts create persons-api-deployer `
  --display-name="PersonsAPI Deployer" `
  --project=TU-PROJECT-ID
```

**Éxito** cuando dice:
```
Created service account [persons-api-deployer].
```

### 7.2 Otorga permiso de escritura a Artifact Registry

```powershell
gcloud projects add-iam-policy-binding TU-PROJECT-ID `
  --member="serviceAccount:persons-api-deployer@TU-PROJECT-ID.iam.gserviceaccount.com" `
  --role="roles/artifactregistry.writer"
```

**Éxito** cuando imprime el JSON de la política IAM actualizada (es largo, no te preocupes por leerlo todo).

### 7.3 Otorga permiso de deploy a Cloud Run

```powershell
gcloud projects add-iam-policy-binding TU-PROJECT-ID `
  --member="serviceAccount:persons-api-deployer@TU-PROJECT-ID.iam.gserviceaccount.com" `
  --role="roles/run.admin"
```

### 7.4 Descarga la clave JSON

Este archivo es la "contraseña" de la service account. **Nunca lo subas a git.**

Primero, **asegúrate de estar en la raíz del proyecto**:
```powershell
cd C:\ATS\Git\platform
```

Descarga la clave:
```powershell
gcloud iam service-accounts keys create key.json `
  --iam-account=persons-api-deployer@TU-PROJECT-ID.iam.gserviceaccount.com
```

**Éxito** cuando dice:
```
created key [xxxxxxxxxxxx] of type [json] as [key.json] for [persons-api-deployer@TU-PROJECT-ID.iam.gserviceaccount.com]
```

Se crea `key.json` en `C:\ATS\Git\platform\`.

### 7.5 Verifica que git NO ve el archivo (CRÍTICO)

```powershell
git status
```

`key.json` **NO debe aparecer** en la lista. Si aparece como "Untracked files", algo está mal con el .gitignore — detente y reporta el problema antes de continuar.

---

## FASE 8: Construir y subir la imagen Docker a GCP

### 8.1 Confirma que estás en la raíz del proyecto

```powershell
pwd
```

Debe mostrar `C:\ATS\Git\platform`. Si no, ejecuta `cd C:\ATS\Git\platform`.

### 8.2 Construye la imagen con el tag de Artifact Registry

```powershell
docker build -t us-central1-docker.pkg.dev/TU-PROJECT-ID/personsapi/personsapi:latest .
```

**Este proceso tarda 2-5 minutos** la primera vez (descarga la imagen base de .NET). Verás muchas líneas de output. Al final debe decir:
```
 => => writing image sha256:...
 => => naming to us-central1-docker.pkg.dev/TU-PROJECT-ID/personsapi/personsapi:latest
```

Si hay errores en este paso, copia el output completo antes de continuar.

### 8.3 Sube la imagen a Artifact Registry

```powershell
docker push us-central1-docker.pkg.dev/TU-PROJECT-ID/personsapi/personsapi:latest
```

**Tarda 1-3 minutos** (sube los layers del contenedor). Verás barras de progreso. Al final:
```
latest: digest: sha256:... size: ...
```

### 8.4 Verifica que la imagen está en GCP

```powershell
gcloud artifacts docker images list us-central1-docker.pkg.dev/TU-PROJECT-ID/personsapi
```

Debe mostrar una fila con tu imagen y el tag `latest`.

---

## FASE 9: Hacer el Deploy a Cloud Run

### 9.1 Ejecuta el deploy

```powershell
gcloud run deploy persons-api `
  --image us-central1-docker.pkg.dev/TU-PROJECT-ID/personsapi/personsapi:latest `
  --region us-central1 `
  --port 8080 `
  --memory 512Mi `
  --cpu 1 `
  --min-instances 0 `
  --allow-unauthenticated `
  --set-env-vars "ASPNETCORE_ENVIRONMENT=Production"
```

> **Advertencia:** Usa `--port 8080` exactamente como está escrito. **No uses `--port 80`.** El contenedor escucha en el puerto 8080. Usar `--port 80` provoca un crash loop.

**Tarda 1-3 minutos.** Verás:
```
Deploying container to Cloud Run service [persons-api] in project [TU-PROJECT-ID] region [us-central1]
✓ Deploying new service... Done.
  ✓ Creating Revision...
  ✓ Routing traffic...
  ✓ Setting IAM Policy...
Done.
Service [persons-api] revision [persons-api-00001-xxx] has been deployed and is serving 100 percent of traffic.
Service URL: https://persons-api-xxxxxxxxxx-uc.a.run.app
```

**Copia esa Service URL** — la necesitas para el siguiente paso.

---

## FASE 10: Verificar que todo funciona (4 criterios de éxito)

Guarda la URL como variable en PowerShell (reemplaza con tu URL real):

```powershell
$SERVICE_URL = "https://persons-api-xxxxxxxxxx-uc.a.run.app"
```

### SC-1: Health check desde internet público

```powershell
curl --max-time 30 "$SERVICE_URL/health"
```

**Esperado:**
```json
{"status":"Healthy"}
```

> Si el primero da 503, espera 5 segundos y repite — es el cold start (el contenedor se estaba despertando desde cero). Si persiste después de 2 intentos, hay un problema de puerto.

### SC-2: Las 3 personas sembradas

```powershell
curl "$SERVICE_URL/api/persons"
```

**Esperado:** Array JSON con 3 personas (Carlos Herrera López, Ana García Martínez, Luis Morales Reyes).

### SC-3: Sin crash loop

```powershell
gcloud run services describe persons-api --region us-central1 --format='value(status.conditions)'
```

Busca en el output que diga `Ready` con valor `True`. No debe aparecer `ContainerFailed` ni `CrashLoopBackOff`.

### SC-4: Logs JSON en Cloud Logging

1. Abre este URL en tu navegador (reemplaza `TU-PROJECT-ID`):
   ```
   https://console.cloud.google.com/logs/query?project=TU-PROJECT-ID
   ```
2. En el campo de consulta, escribe exactamente:
   ```
   resource.type="cloud_run_revision" AND resource.labels.service_name="persons-api"
   ```
3. Presiona **"Run Query"**

Debes ver entradas de log en formato JSON. Las entradas tienen severity "DEFAULT" (sin colores de severity) — eso es normal y esperado para Serilog CLEF.

---

## Apéndice: Limpieza / Teardown

Si quieres eliminar todos los recursos y dejar de incurrir cualquier costo en GCP:

### Eliminar el servicio de Cloud Run

```powershell
gcloud run services delete persons-api --region us-central1 --quiet
```

### Eliminar el repositorio de Artifact Registry (y todas las imágenes)

```powershell
gcloud artifacts repositories delete personsapi `
  --location=us-central1 `
  --quiet
```

### Eliminar la service account

```powershell
gcloud iam service-accounts delete `
  persons-api-deployer@TU-PROJECT-ID.iam.gserviceaccount.com `
  --quiet
```

### Eliminar el proyecto GCP completo

```powershell
gcloud projects delete TU-PROJECT-ID
```

> **Advertencia:** Eliminar el proyecto borra todos los recursos permanentemente y no se puede deshacer. Perderás el historial de facturación y el crédito de $300 si no lo usaste completamente.

---

*PersonsAPI v2.0 — Cloud Run Deployment — Guía en español*
