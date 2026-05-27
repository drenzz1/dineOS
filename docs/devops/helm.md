# Helm — Kubernetes Deployment

## Overview

The Helm chart at `deploy/helm/dineos` deploys the full dineOS platform to any Kubernetes cluster.  
It covers the API (DineOS.Api) and the Next.js frontend, an Nginx Ingress, a chart-managed Secret, and optional in-cluster dependencies (PostgreSQL, Redis, RabbitMQ, Keycloak) that can be toggled off when pointing at managed cloud services.

| File | Purpose |
|------|---------|
| `values.yaml` | Production-shaped defaults — all dependencies disabled, TLS enabled, no in-cluster infra |
| `values.local.yaml` | minikube / kind overrides — all dependencies enabled, no TLS, dev credentials |

All commands in this document are run from the **repo root**.

---

## Prerequisites

| Tool | Version | Check |
|------|---------|-------|
| `kubectl` | matches your cluster server version | `kubectl version --client` |
| `helm` | 3.14 or later | `helm version` |
| `minikube` or `kind` | any recent | local clusters only — skip for cloud |

For local clusters you also need the **nginx-ingress-controller** add-on:

```bash
# minikube
minikube addons enable ingress

# kind — apply the official manifest
kubectl apply -f https://raw.githubusercontent.com/kubernetes/ingress-nginx/main/deploy/static/provider/kind/deploy.yaml
```

For production clusters, **cert-manager** must be installed before the Ingress can issue TLS certificates:

```bash
kubectl apply -f https://github.com/cert-manager/cert-manager/releases/latest/download/cert-manager.yaml
```

---

## First-Time Setup

### 1. Add Helm repositories

```bash
helm repo add bitnami https://charts.bitnami.com/bitnami
helm repo add codecentric https://codecentric.github.io/helm-charts
helm repo update
```

### 2. Fetch sub-charts

```bash
helm dependency update deploy/helm/dineos
```

This downloads the Bitnami (PostgreSQL, Redis, RabbitMQ) and Codecentric (Keycloak) sub-chart tarballs into `deploy/helm/dineos/charts/` and writes `Chart.lock`.  
Run this again any time `Chart.yaml` changes.

### 3. Install

**Local cluster (minikube / kind):**

```bash
helm install dineos deploy/helm/dineos \
  -f deploy/helm/dineos/values.local.yaml \
  -n dineos \
  --create-namespace
```

**Production cluster:**

```bash
helm install dineos deploy/helm/dineos \
  -f deploy/helm/dineos/values.yaml \
  --set secrets.postgresPassword="..." \
  --set secrets.keycloakClientSecret="..." \
  --set secrets.keycloakAdminClientSecret="..." \
  --set secrets.jwtSigningKey="..." \
  --set secrets.smtpPassword="..." \
  --set secrets.rabbitMqPassword="..." \
  --set "secrets.providerApiKeys.stripe=..." \
  --set "secrets.providerApiKeys.stripeWebhook=..." \
  --set "secrets.providerApiKeys.anthropic=..." \
  -n dineos \
  --create-namespace
```

The first install pulls images and starts all Pods. Allow 2–3 minutes for the in-cluster dependencies (Keycloak in particular) to become ready.

---

## Service URL Map

After install, resolve the Ingress address:

```bash
kubectl get ingress -n dineos
```

| Route | Backend | Notes |
|-------|---------|-------|
| `/` | `dineos-frontend:80` | Next.js frontend |
| `/api` | `dineos-api:80` | REST API — all versioned endpoints |
| `/hubs` | `dineos-api:80` | SignalR WebSocket connections |
| `/uploads` | `dineos-api:80` | Static file uploads (50 MB limit) |
| `/swagger` | `dineos-api:80` | Swagger UI — local only (`ingress.swagger.enabled=true`) |

For local clusters, add the Ingress IP to `/etc/hosts` (or `C:\Windows\System32\drivers\etc\hosts`):

```
<minikube-ip>  dineos.local
```

Get the minikube IP with `minikube ip`.

---

## Credentials (Local Dev Only)

> These are the dev defaults from `values.local.yaml`. Do not use them in production.

| Service | Username | Password |
|---------|----------|----------|
| Postgres | `dineos` | `dineos_dev` |
| RabbitMQ | `dineos` | `dineos_dev` |
| Keycloak admin console | `admin` | `admin` |

Seeded application users (imported from `backend/keycloak/realm-export.json`):

| Role | Email | Password |
|------|-------|----------|
| Manager | `admin@dineos.dev` | `Test1234!` |
| Cashier | `cashier@dineos.dev` | `Test1234!` |
| Kitchen Staff | `kitchen@dineos.dev` | `Test1234!` |

---

## Lifecycle Commands

### Redeploy after code or values changes

```bash
helm upgrade --install dineos deploy/helm/dineos \
  -f deploy/helm/dineos/values.local.yaml \
  -n dineos
```

`--install` makes this command safe to run on first deploy and on every subsequent update — there is no separate `install` / `upgrade` step in CI.

### Check release status

```bash
helm status dineos -n dineos
helm history dineos -n dineos
```

### Roll back to a previous revision

```bash
# List available revisions
helm history dineos -n dineos

# Roll back to a specific revision
helm rollback dineos <REVISION> -n dineos
```

Helm rolls back both the Kubernetes resources and the stored release secret, so `helm history` reflects the rollback as a new revision entry.

### Uninstall

```bash
# Remove the release (Pods, Services, Ingress, ConfigMaps, Secrets)
helm uninstall dineos -n dineos

# Also delete the namespace and all PVCs
kubectl delete namespace dineos
```

---

## Managing Secrets

### Option A — chart-managed Secret (default)

`secrets.create=true` (the default) renders a Kubernetes Secret from the values you pass at install time.  
Secret values are never stored in `values.yaml` — always supply them via `--set` or a private values overlay that is **not committed to the repository**.

### Option B — pre-created Secret

Use this when you manage secrets externally (Sealed Secrets, External Secrets Operator, HashiCorp Vault, etc.).

**Step 1.** Create the Secret before installing the chart:

```bash
kubectl create secret generic dineos-secrets \
  --from-literal=ConnectionStrings__DefaultConnection="Host=<host>;Port=5432;Database=dineos;Username=dineos;Password=<pw>" \
  --from-literal=Keycloak__ClientSecret="<client-secret>" \
  --from-literal=Keycloak__AdminClientSecret="<admin-secret>" \
  --from-literal=Jwt__SigningKey="<signing-key>" \
  --from-literal=Smtp__Password="<smtp-password>" \
  --from-literal=RabbitMq__Password="<rabbitmq-password>" \
  --from-literal=Stripe__SecretKey="<stripe-key>" \
  --from-literal=Stripe__WebhookSecret="<stripe-webhook-secret>" \
  --from-literal=Anthropic__ApiKey="<anthropic-key>" \
  --from-literal=OpenAI__ApiKey="<openai-key>" \
  --from-literal=GoogleAI__ApiKey="<googleai-key>" \
  -n dineos
```

**Step 2.** Install the chart pointing at the pre-existing Secret:

```bash
helm install dineos deploy/helm/dineos \
  -f deploy/helm/dineos/values.yaml \
  --set secrets.create=false \
  --set secrets.existingSecret=dineos-secrets \
  -n dineos \
  --create-namespace
```

The `dineos.secretName` helper in `_helpers.tpl` resolves to `secrets.existingSecret` when set, so all workload `envFrom` references automatically pick up the right Secret name.

---

## Rendering Manifests Offline

Use `helm template` to render the full manifest without a live cluster — useful for auditing, GitOps pipelines, or dry-running a values change:

```bash
helm template dineos deploy/helm/dineos \
  --values deploy/helm/dineos/values.local.yaml \
  > rendered.yaml
```

Inspect a single resource type:

```bash
helm template dineos deploy/helm/dineos \
  --values deploy/helm/dineos/values.local.yaml \
  --show-only templates/secret.yaml
```

Validate against the cluster's API schema (requires a live cluster):

```bash
helm template dineos deploy/helm/dineos \
  --values deploy/helm/dineos/values.local.yaml \
  | kubectl apply --dry-run=server -f -
```

---

## Troubleshooting

### ImagePullBackOff

```bash
kubectl describe pod -n dineos -l app.kubernetes.io/component=api
```

Look for `Failed to pull image` in the events section.

**Local cluster (minikube):** images must be loaded into the cluster before install — Helm cannot pull `pullPolicy: Never` images from a registry.

```bash
# Build locally (from repo root)
docker build -t dineos/api:do2 -f backend/src/DineOS.Api/Dockerfile ./backend
docker build -t dineos/web:do2 ./frontend

# Load into minikube
minikube image load dineos/api:do2
minikube image load dineos/web:do2
```

For **kind**:

```bash
kind load docker-image dineos/api:do2
kind load docker-image dineos/web:do2
```

**Production cluster:** confirm `image.tag` in your values overlay matches a tag that exists in your registry and that `imagePullSecrets` is configured if the registry is private.

---

### Keycloak realm import not applied

The Keycloak sub-chart starts in `start-dev --import-realm` mode. When `dependencies.keycloak.enabled=true`, the chart automatically:

1. Renders a `<release>-keycloak-realm` ConfigMap containing the full realm definition from `deploy/helm/dineos/files/realm-export.json`.
2. Mounts that ConfigMap into the Keycloak Pod at `/opt/keycloak/data/import/` via the `keycloak.extraVolumes` / `extraVolumeMounts` values.

If the realm is not present after Keycloak starts, diagnose with:

1. Confirm the Pod started without errors:
   ```bash
   kubectl logs -n dineos -l app.kubernetes.io/name=keycloak --tail=80
   ```
2. Confirm the import file is visible inside the Pod:
   ```bash
   kubectl exec -n dineos statefulset/dineos-keycloak -- \
     ls /opt/keycloak/data/import/
   ```
   You should see `realm-export.json`. If the directory is empty, the ConfigMap was not mounted — check that `dependencies.keycloak.enabled=true` in your values overlay (the ConfigMap and volume are both gated on this flag).
3. Confirm the ConfigMap exists in the cluster:
   ```bash
   kubectl get configmap -n dineos dineos-keycloak-realm
   ```

Keycloak can take **30–90 seconds** on first boot before it is ready to validate tokens. The API's readiness probe will fail until Keycloak's OIDC metadata endpoint responds. This is expected — wait for all Pods to show `Running` and `Ready 1/1`:

```bash
kubectl get pods -n dineos -w
```

---

### Ingress TLS certificate not issued

After install, cert-manager attempts to provision a certificate for the host in `ingress.tls[0].hosts`.  
Check the Certificate and CertificateRequest resources:

```bash
kubectl describe certificate -n dineos
kubectl describe certificaterequest -n dineos
```

Common causes and fixes:

| Symptom | Cause | Fix |
|---------|-------|-----|
| `Waiting for HTTP-01 challenge` | DNS for the Ingress host not yet propagated | Wait for DNS; verify with `nslookup <host>` |
| `ClusterIssuer not found` | cert-manager not installed or issuer name mismatch | Install cert-manager; confirm `ingress.annotations.cert-manager.io/cluster-issuer` matches an existing `ClusterIssuer` name |
| `ACME account not registered` | ClusterIssuer email not set or Let's Encrypt rate-limited | Check the `ClusterIssuer` object; use the Let's Encrypt staging issuer for testing |
| Certificate stays `False` | Ingress controller not responding on port 80 | Confirm the nginx-ingress-controller Pod is running and port 80 is reachable from the internet |

To bypass TLS during initial setup, set `ingress.tls: []` and `ingress.annotations."nginx.ingress.kubernetes.io/ssl-redirect": "false"` in a temporary overlay, then re-enable once DNS and the issuer are confirmed working.

---

## Verification

Three levels of verification are available, ordered by cost:

| Target | Requires | What it checks |
|--------|----------|----------------|
| `make helm-lint` | helm only | Chart structure, template syntax, required values |
| `make helm-dry-run` | helm + kubectl | Every rendered manifest is valid Kubernetes YAML |
| `make helm-smoke` | kind, docker, helm, kubectl | Full install → rollout → live HTTP check → teardown |

Run `make helm-verify` to execute lint and dry-run together (no cluster needed).

---

### Step 1 — `helm lint`

```bash
make helm-lint
# or directly:
helm lint deploy/helm/dineos --values deploy/helm/dineos/values.local.yaml
```

**Expected output:**

```
==> Linting deploy/helm/dineos
[INFO] Chart.yaml: icon is recommended

1 chart(s) linted, 0 chart(s) failed
```

The `icon is recommended` info message is expected and non-blocking — it does not fail the lint.  
Any `[ERROR]` or `[WARNING]` line indicates a real problem.

---

### Step 2 — offline schema validation with kubeconform

```bash
make helm-dry-run
# or directly:
helm template dineos deploy/helm/dineos \
  --values deploy/helm/dineos/values.local.yaml \
| kubeconform \
    -strict \
    -summary \
    -kubernetes-version 1.31.0
```

`kubeconform` validates every rendered resource against the Kubernetes 1.31 schema without touching a live cluster.  
Install: `https://github.com/yannh/kubeconform/releases`

> **Why not `kubectl apply --dry-run=client`?**  
> kubectl 1.33+ unconditionally contacts the API server for group discovery even with `--validate=false`, making the flag unusable without a running cluster. kubeconform is the correct offline alternative.

**Expected output:**

```
Summary: 40 resources found in 1 file - Valid: 40, Invalid: 0, Errors: 0, Skipped: 0
```

If a template reference is wrong or a required values key is missing, kubeconform will print a validation error and exit non-zero before the summary line.

---

### Step 3 — kind smoke test

```bash
make helm-smoke
# or directly:
bash scripts/devops/verify-helm.sh
```

The script performs these steps in order, tearing the cluster down on exit regardless of success or failure:

| Step | Command | Pass condition |
|------|---------|----------------|
| 1 | `kind create cluster --name dineos-test` | Cluster created, kubeconfig set |
| 2 | Apply nginx-ingress-controller manifest | Controller Pod `Ready` within 90 s |
| 3 | `docker build` API + frontend images | Exit 0 |
| 4 | `kind load docker-image` for both images | Images available inside kind nodes |
| 5 | `helm dependency update` | `Chart.lock` up to date |
| 6 | `helm install dineos` with `values.local.yaml` | Release `STATUS: deployed` |
| 7 | `kubectl rollout status` for both Deployments | `successfully rolled out` within 3 min |
| 8 | `curl` via port-forwarded ingress — `/api/v1/health` | HTTP 200, body contains `"status":"healthy"` |
| 9 | `curl` via port-forwarded ingress — `/` | HTTP 200 (frontend) |
| 10 | `kind delete cluster --name dineos-test` | Cluster removed |

**Expected output (condensed):**

```
→ [1/10] Creating kind cluster 'dineos-test'
Creating cluster "dineos-test" ...
 ✓ Ensuring node image (kindest/node:v1.31.0) 🖼
 ✓ Preparing nodes 📦
 ✓ Writing configuration 📜
 ✓ Starting control-plane 🕹️
 ✓ Installing CNI 🔌
 ✓ Installing StorageClass 💾
Set kubectl context to "kind-dineos-test"

→ [2/10] Installing nginx-ingress-controller
...
pod/ingress-nginx-controller-7d5b8d7d69-x9zrp condition met

→ [3/10] Building images
...
=> exporting to image                                           0.3s
=> => naming to docker.io/dineos/api:do2                       0.0s
...
=> => naming to docker.io/dineos/web:do2                       0.0s

→ [4/10] Loading images into kind nodes
Image: "dineos/api:do2" with ID "sha256:3f8a..." loaded in 1 node(s)
Image: "dineos/web:do2" with ID "sha256:9c2d..." loaded in 1 node(s)

→ [5/10] Updating Helm dependencies
Saving 4 charts
Deleting outdated charts

→ [6/10] Installing Helm chart
NAME: dineos
LAST DEPLOYED: Tue May 27 12:34:56 2025
NAMESPACE: dineos
STATUS: deployed
REVISION: 1
NOTES:
dineOS has been deployed successfully!
Application URL: http://dineos.local

→ [7/10] Waiting for rollouts
Waiting for deployment "dineos-api" rollout to finish: 0 of 1 updated replicas are available...
deployment.apps/dineos-api successfully rolled out
Waiting for deployment "dineos-frontend" rollout to finish: 0 of 1 updated replicas are available...
deployment.apps/dineos-frontend successfully rolled out

→ [8/10] Health check — GET /api/v1/health
{"status":"healthy","timestamp":"2025-05-27T12:36:01Z","version":"0.1.0"}

→ [9/10] Frontend check — GET /
HTTP 200

→ [10/10] Tearing down kind cluster
Deleting cluster "dineos-test" ...

✓ helm-smoke passed (all 10 checks)
```

If any step fails the script exits immediately, prints a failure summary, and still deletes the kind cluster via the `trap EXIT` handler so no orphaned clusters are left behind.
