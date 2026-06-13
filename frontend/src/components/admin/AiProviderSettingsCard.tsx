"use client";

import { useState } from "react";
import { Card } from "@/components/ui/Card";
import { Button } from "@/components/ui/Button";
import {
  useAiSettings,
  useSaveAiSettings,
  useSaveEmbeddingsSettings,
  useTestAiConnection,
} from "@/hooks/useAiSettings";
import type { AiProvider, EmbeddingsProvider } from "@/types/admin";

const PROVIDERS: { value: AiProvider; label: string; hint: string }[] = [
  { value: "Anthropic", label: "Anthropic (Claude)", hint: "Starts with sk-ant-" },
  { value: "OpenAI",    label: "OpenAI (GPT)",       hint: "Starts with sk-" },
  { value: "Google",    label: "Google (Gemini)",    hint: "Starts with AI" },
];

const EMBEDDINGS_PROVIDERS: { value: EmbeddingsProvider; label: string; hint: string }[] = [
  { value: "OpenAI",  label: "OpenAI",  hint: "Starts with sk-" },
  { value: "Google",  label: "Google",  hint: "Starts with AI" },
];

function StatusPill({ success, model }: { success: boolean; model?: string | null }) {
  return (
    <span
      className={`inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-xs font-semibold ${
        success
          ? "bg-status-ready-bg text-success border border-status-ready-border"
          : "bg-status-cancelled-bg text-danger border border-status-cancelled-border"
      }`}
    >
      <span className={`h-1.5 w-1.5 rounded-full ${success ? "bg-success" : "bg-danger"}`} />
      {success ? `Connected${model ? ` · ${model}` : ""}` : "Connection failed"}
    </span>
  );
}

export default function AiProviderSettingsCard() {
  const { data: settings, isLoading } = useAiSettings();
  const saveMutation      = useSaveAiSettings();
  const saveEmbMutation   = useSaveEmbeddingsSettings();
  const testMutation      = useTestAiConnection();

  // Chat provider
  const [selectedProvider, setSelectedProvider] = useState<AiProvider | null>(null);
  const provider: AiProvider = selectedProvider ?? settings?.activeProvider ?? "Anthropic";
  const [apiKey,  setApiKey]  = useState("");
  const [showKey, setShowKey] = useState(false);

  // Embeddings provider
  const [selectedEmbProvider, setSelectedEmbProvider] = useState<EmbeddingsProvider | null>(null);
  const embProvider: EmbeddingsProvider =
    selectedEmbProvider ?? (settings?.embeddingsProvider as EmbeddingsProvider | undefined) ?? "OpenAI";
  const [embApiKey,  setEmbApiKey]  = useState("");
  const [showEmbKey, setShowEmbKey] = useState(false);

  const currentHint =
    provider === "Anthropic" ? settings?.anthropicApiKeyHint
    : provider === "OpenAI"  ? settings?.openAiApiKeyHint
    :                           settings?.googleAiApiKeyHint;

  function handleTest() {
    if (!apiKey.trim()) return;
    testMutation.reset();
    testMutation.mutate({ provider, apiKey: apiKey.trim() });
  }

  function handleSave() {
    if (!apiKey.trim()) return;
    saveMutation.reset();
    testMutation.reset();
    saveMutation.mutate(
      { provider, apiKey: apiKey.trim() },
      { onSuccess: () => setApiKey("") }
    );
  }

  function handleSaveEmbeddings() {
    if (!embApiKey.trim()) return;
    saveEmbMutation.reset();
    saveEmbMutation.mutate(
      { provider: embProvider, apiKey: embApiKey.trim() },
      { onSuccess: () => setEmbApiKey("") }
    );
  }

  if (isLoading) {
    return (
      <Card className="animate-pulse space-y-3">
        <div className="h-4 w-48 rounded bg-surface-3" />
        <div className="h-8 w-full rounded bg-surface-2" />
        <div className="h-8 w-full rounded bg-surface-2" />
      </Card>
    );
  }

  return (
    <Card>
      {/* ── Chat provider ──────────────────────────────────────────── */}
      <div className="mb-4">
        <h2 className="text-sm font-semibold text-fg">AI Provider Settings</h2>
        <p className="mt-0.5 text-xs text-fg-subtle">
          Choose a provider and paste your API key. The key is stored securely and used for all AI features.
        </p>
        {settings?.updatedAt && (
          <p className="mt-1 text-[11px] text-fg-subtle">
            Last updated {new Date(settings.updatedAt).toLocaleString()}
          </p>
        )}
      </div>

      <div className="mb-4 space-y-2">
        <p className="text-xs font-medium text-fg-muted">Chat Provider</p>
        <div className="flex flex-col gap-2 sm:flex-row">
          {PROVIDERS.map((p) => {
            const isActive = provider === p.value;
            const savedHint =
              p.value === "Anthropic" ? settings?.anthropicApiKeyHint
              : p.value === "OpenAI"  ? settings?.openAiApiKeyHint
              :                          settings?.googleAiApiKeyHint;
            return (
              <button
                key={p.value}
                type="button"
                onClick={() => { setSelectedProvider(p.value); setApiKey(""); testMutation.reset(); }}
                className={`flex flex-1 flex-col rounded-md border px-3 py-2.5 text-left transition-colors ${
                  isActive
                    ? "border-accent bg-accent-soft text-accent"
                    : "border-border bg-surface text-fg-muted hover:border-border-strong hover:text-fg"
                }`}
              >
                <span className="text-[13px] font-medium">{p.label}</span>
                <span className="mt-0.5 text-[11px] opacity-70">
                  {savedHint ? `Saved: ${savedHint}` : "Not configured"}
                </span>
              </button>
            );
          })}
        </div>
      </div>

      <div className="mb-4">
        <p className="mb-1.5 text-xs font-medium text-fg-muted">
          API Key {currentHint && <span className="text-fg-subtle">({currentHint} — enter new to replace)</span>}
        </p>
        <div className="flex gap-2">
          <div className="relative flex-1">
            <input
              type={showKey ? "text" : "password"}
              value={apiKey}
              onChange={(e) => { setApiKey(e.target.value); testMutation.reset(); }}
              placeholder={currentHint ? "Enter new key to replace saved one" : PROVIDERS.find(p => p.value === provider)?.hint ?? "Paste API key here"}
              className="w-full rounded-md border border-border bg-surface px-3 py-2 pr-10 text-sm text-fg placeholder:text-fg-muted focus:border-accent focus:outline-none focus:ring-1 focus:ring-accent"
            />
            <button
              type="button"
              onClick={() => setShowKey((v) => !v)}
              className="absolute right-2.5 top-1/2 -translate-y-1/2 text-xs text-fg-subtle hover:text-fg-muted"
            >
              {showKey ? "Hide" : "Show"}
            </button>
          </div>
        </div>
      </div>

      {testMutation.data && (
        <div className="mb-4 flex items-center gap-2">
          <StatusPill success={testMutation.data.success} model={testMutation.data.model} />
          {!testMutation.data.success && testMutation.data.error && (
            <p className="text-xs text-danger">{testMutation.data.error}</p>
          )}
        </div>
      )}

      {saveMutation.isError && (
        <p className="mb-3 text-xs text-danger">Failed to save settings. Please try again.</p>
      )}
      {saveMutation.isSuccess && (
        <p className="mb-3 text-xs text-success">Settings saved successfully.</p>
      )}

      <div className="flex gap-2">
        <Button variant="secondary" size="sm" isLoading={testMutation.isPending} disabled={!apiKey.trim()} onClick={handleTest}>
          Test Connection
        </Button>
        <Button variant="primary" size="sm" isLoading={saveMutation.isPending} disabled={!apiKey.trim()} onClick={handleSave}>
          Save
        </Button>
      </div>

      {/* ── Embeddings provider (semantic search) ──────────────────── */}
      <div className="mt-6 border-t border-border pt-6">
        <h3 className="text-sm font-semibold text-fg">Semantic Search (Embeddings)</h3>
        <p className="mt-0.5 text-xs text-fg-subtle">
          Used for menu item semantic search. Anthropic is not supported for embeddings.
          {settings?.embeddingsProvider
            ? <span className="ml-1 text-fg-subtle">Current: <span className="font-medium">{settings.embeddingsProvider}</span></span>
            : <span className="ml-1 text-fg-subtle">Not configured — semantic search is disabled.</span>
          }
        </p>
      </div>

      <div className="mt-3 mb-4 space-y-2">
        <p className="text-xs font-medium text-fg-muted">Embeddings Provider</p>
        <div className="flex flex-col gap-2 sm:flex-row">
          {EMBEDDINGS_PROVIDERS.map((p) => {
            const isActive = embProvider === p.value;
            const isSaved  = settings?.embeddingsProvider === p.value;
            return (
              <button
                key={p.value}
                type="button"
                onClick={() => { setSelectedEmbProvider(p.value); setEmbApiKey(""); saveEmbMutation.reset(); }}
                className={`flex flex-1 flex-col rounded-md border px-3 py-2.5 text-left transition-colors ${
                  isActive
                    ? "border-accent bg-accent-soft text-accent"
                    : "border-border bg-surface text-fg-muted hover:border-border-strong hover:text-fg"
                }`}
              >
                <span className="text-[13px] font-medium">{p.label}</span>
                <span className="mt-0.5 text-[11px] opacity-70">
                  {isSaved && settings?.embeddingsApiKeyHint
                    ? `Saved: ${settings.embeddingsApiKeyHint}`
                    : "Not configured"}
                </span>
              </button>
            );
          })}
        </div>
      </div>

      <div className="mb-4">
        <p className="mb-1.5 text-xs font-medium text-fg-muted">
          Embeddings API Key
          {settings?.embeddingsApiKeyHint && settings.embeddingsProvider === embProvider && (
            <span className="text-fg-subtle"> ({settings.embeddingsApiKeyHint} — enter new to replace)</span>
          )}
        </p>
        <div className="relative">
          <input
            type={showEmbKey ? "text" : "password"}
            value={embApiKey}
            onChange={(e) => { setEmbApiKey(e.target.value); saveEmbMutation.reset(); }}
            placeholder={EMBEDDINGS_PROVIDERS.find(p => p.value === embProvider)?.hint ?? "Paste API key here"}
            className="w-full rounded-md border border-border bg-surface px-3 py-2 pr-10 text-sm text-fg placeholder:text-fg-muted focus:border-accent focus:outline-none focus:ring-1 focus:ring-accent"
          />
          <button
            type="button"
            onClick={() => setShowEmbKey((v) => !v)}
            className="absolute right-2.5 top-1/2 -translate-y-1/2 text-xs text-fg-subtle hover:text-fg-muted"
          >
            {showEmbKey ? "Hide" : "Show"}
          </button>
        </div>
      </div>

      {saveEmbMutation.isError && (
        <p className="mb-3 text-xs text-danger">Failed to save embeddings settings. Please try again.</p>
      )}
      {saveEmbMutation.isSuccess && (
        <p className="mb-3 text-xs text-success">Embeddings settings saved.</p>
      )}

      <Button
        variant="primary"
        size="sm"
        isLoading={saveEmbMutation.isPending}
        disabled={!embApiKey.trim()}
        onClick={handleSaveEmbeddings}
      >
        Save Embeddings Key
      </Button>
    </Card>
  );
}
