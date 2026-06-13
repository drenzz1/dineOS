"use client";

import { useState, useEffect } from "react";
import { Card } from "@/components/ui/Card";
import { Button } from "@/components/ui/Button";
import { useAiSettings, useSaveAiSettings, useTestAiConnection } from "@/hooks/useAiSettings";
import type { AiProvider } from "@/types/admin";

const PROVIDERS: { value: AiProvider; label: string; hint: string }[] = [
  { value: "Anthropic", label: "Anthropic (Claude)", hint: "Starts with sk-ant-" },
  { value: "OpenAI",    label: "OpenAI (GPT)",       hint: "Starts with sk-" },
  { value: "Google",    label: "Google (Gemini)",    hint: "Starts with AI" },
];

function StatusPill({ success, model }: { success: boolean; model?: string | null }) {
  return (
    <span
      className={`inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-xs font-semibold ${
        success
          ? "bg-green-50 text-green-700 border border-green-200"
          : "bg-red-50 text-red-700 border border-red-200"
      }`}
    >
      <span
        className={`h-1.5 w-1.5 rounded-full ${success ? "bg-green-500" : "bg-red-500"}`}
      />
      {success ? `Connected${model ? ` · ${model}` : ""}` : "Connection failed"}
    </span>
  );
}

export default function AiProviderSettingsCard() {
  const { data: settings, isLoading } = useAiSettings();
  const saveMutation  = useSaveAiSettings();
  const testMutation  = useTestAiConnection();

  const [provider, setProvider] = useState<AiProvider>("Anthropic");
  const [apiKey,   setApiKey]   = useState("");
  const [showKey,  setShowKey]  = useState(false);

  useEffect(() => {
    if (settings) setProvider(settings.activeProvider);
  }, [settings]);

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

  if (isLoading) {
    return (
      <Card className="animate-pulse space-y-3">
        <div className="h-4 w-48 rounded bg-zinc-200" />
        <div className="h-8 w-full rounded bg-zinc-100" />
        <div className="h-8 w-full rounded bg-zinc-100" />
      </Card>
    );
  }

  return (
    <Card>
      <div className="mb-4">
        <h2 className="text-sm font-semibold text-zinc-900">AI Provider Settings</h2>
        <p className="mt-0.5 text-xs text-zinc-500">
          Choose a provider and paste your API key. The key is stored securely and used for all AI features.
        </p>
        {settings?.updatedAt && (
          <p className="mt-1 text-[11px] text-zinc-400">
            Last updated {new Date(settings.updatedAt).toLocaleString()}
          </p>
        )}
      </div>

      {/* Provider selector */}
      <div className="mb-4 space-y-2">
        <p className="text-xs font-medium text-zinc-700">Provider</p>
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
                onClick={() => { setProvider(p.value); setApiKey(""); testMutation.reset(); }}
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

      {/* API key input */}
      <div className="mb-4">
        <p className="mb-1.5 text-xs font-medium text-zinc-700">
          API Key {currentHint && <span className="text-zinc-400">({currentHint} — enter new to replace)</span>}
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
              className="absolute right-2.5 top-1/2 -translate-y-1/2 text-xs text-zinc-400 hover:text-zinc-600"
            >
              {showKey ? "Hide" : "Show"}
            </button>
          </div>
        </div>
      </div>

      {/* Test result */}
      {testMutation.data && (
        <div className="mb-4 flex items-center gap-2">
          <StatusPill success={testMutation.data.success} model={testMutation.data.model} />
          {!testMutation.data.success && testMutation.data.error && (
            <p className="text-xs text-red-600">{testMutation.data.error}</p>
          )}
        </div>
      )}

      {/* Save error */}
      {saveMutation.isError && (
        <p className="mb-3 text-xs text-red-600">Failed to save settings. Please try again.</p>
      )}
      {saveMutation.isSuccess && (
        <p className="mb-3 text-xs text-green-600">Settings saved successfully.</p>
      )}

      {/* Actions */}
      <div className="flex gap-2">
        <Button
          variant="secondary"
          size="sm"
          isLoading={testMutation.isPending}
          disabled={!apiKey.trim()}
          onClick={handleTest}
        >
          Test Connection
        </Button>
        <Button
          variant="primary"
          size="sm"
          isLoading={saveMutation.isPending}
          disabled={!apiKey.trim()}
          onClick={handleSave}
        >
          Save
        </Button>
      </div>
    </Card>
  );
}
