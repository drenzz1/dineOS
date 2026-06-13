"use client";

import { useState } from "react";
import { Card } from "@/components/ui/Card";
import { Button } from "@/components/ui/Button";
import { useAdminAiBillingInsight } from "@/hooks/useAdminAiBillingInsight";
import type { AdminBillingInsight } from "@/types/admin";

function InsightMeta({ insight }: { insight: AdminBillingInsight }) {
  return (
    <p className="mt-3 text-xs text-fg-subtle">
      {insight.metadata.model} &middot; {insight.metadata.inputTokens + insight.metadata.outputTokens} tokens &middot;{" "}
      {insight.metadata.latencyMs}ms
    </p>
  );
}

export default function AiBillingInsightCard() {
  const { mutate, data, isPending, isError, error } = useAdminAiBillingInsight();
  const [expanded, setExpanded] = useState(false);

  const errorMessage = (() => {
    if (!isError || !error) return null;
    const status = (error as { status?: number }).status;
    if (status === 429) return "Rate limit reached — try again in a minute.";
    return "AI service is temporarily unavailable.";
  })();

  return (
    <Card>
      <div className="flex items-center justify-between">
        <div>
          <p className="text-sm font-semibold text-fg">AI Platform Summary</p>
          <p className="text-xs text-fg-subtle">
            Generate a natural-language analysis of billing and growth data.
          </p>
        </div>
        <Button
          variant="secondary"
          size="sm"
          isLoading={isPending}
          onClick={() => {
            mutate(undefined, { onSuccess: () => setExpanded(true) });
          }}
        >
          {data ? "Regenerate" : "Generate Summary"}
        </Button>
      </div>

      {errorMessage && (
        <p className="mt-3 text-sm text-danger">{errorMessage}</p>
      )}

      {data && (
        <div className="mt-4">
          <button
            type="button"
            className="flex w-full items-center justify-between text-left"
            onClick={() => setExpanded((v) => !v)}
            aria-expanded={expanded}
            aria-label={expanded ? "Hide AI summary" : "Show AI summary"}
          >
            <span className="text-xs font-semibold uppercase tracking-wide text-fg-subtle">
              {expanded ? "Hide" : "Show"} Summary
            </span>
            <span className="text-xs text-fg-subtle">{expanded ? "▲" : "▼"}</span>
          </button>

          {expanded && (
            <div className="mt-3">
              <p className="whitespace-pre-wrap text-sm leading-relaxed text-fg-muted">
                {data.narrative}
              </p>
              <InsightMeta insight={data} />
            </div>
          )}
        </div>
      )}
    </Card>
  );
}
