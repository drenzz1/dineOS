{{/*
Expand the name of the chart.
*/}}
{{- define "dineos.name" -}}
{{- default .Chart.Name .Values.nameOverride | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Create a default fully qualified app name.
Truncate at 63 chars because some Kubernetes name fields are limited to this (DNS naming spec).
*/}}
{{- define "dineos.fullname" -}}
{{- if .Values.fullnameOverride }}
{{- .Values.fullnameOverride | trunc 63 | trimSuffix "-" }}
{{- else }}
{{- $name := default .Chart.Name .Values.nameOverride }}
{{- if contains $name .Release.Name }}
{{- .Release.Name | trunc 63 | trimSuffix "-" }}
{{- else }}
{{- printf "%s-%s" .Release.Name $name | trunc 63 | trimSuffix "-" }}
{{- end }}
{{- end }}
{{- end }}

{{/*
Common labels attached to every resource.
*/}}
{{- define "dineos.labels" -}}
helm.sh/chart: {{ printf "%s-%s" .Chart.Name .Chart.Version | replace "+" "_" | trunc 63 | trimSuffix "-" }}
{{ include "dineos.selectorLabels" . }}
{{- if .Chart.AppVersion }}
app.kubernetes.io/version: {{ .Chart.AppVersion | quote }}
{{- end }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
{{- end }}

{{/*
Selector labels — used in matchLabels / Service selectors.
*/}}
{{- define "dineos.selectorLabels" -}}
app.kubernetes.io/name: {{ include "dineos.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
{{- end }}

{{/*
Service-account name to use for workloads.
*/}}
{{- define "dineos.serviceAccountName" -}}
{{- if .Values.serviceAccount.create }}
{{- default (include "dineos.fullname" .) .Values.serviceAccount.name }}
{{- else }}
{{- default "default" .Values.serviceAccount.name }}
{{- end }}
{{- end }}

{{/*
Name of the Secret mounted by workloads.
Set secrets.existingSecret to the name of a pre-created Secret when
secrets.create=false (e.g. managed via Sealed Secrets, ESO, or Vault).
*/}}
{{- define "dineos.secretName" -}}
{{- .Values.secrets.existingSecret | default (printf "%s-secrets" (include "dineos.fullname" .)) }}
{{- end }}
