import type { ReactNode } from "react";

interface EmptyStateProps {
  illustration?: ReactNode;
  title: string;
  description?: string;
  cta?: ReactNode;
  compact?: boolean;
  className?: string;
}

function mergeClasses(...classes: Array<string | undefined | false>): string {
  return classes.filter(Boolean).join(" ");
}

export function EmptyState({
  illustration,
  title,
  description,
  cta,
  compact = false,
  className,
}: EmptyStateProps) {
  return (
    <div
      className={mergeClasses(
        "flex flex-col items-center justify-center text-center text-fg",
        compact ? "py-6 px-4" : "py-12 px-6",
        className,
      )}
    >
      {illustration && <div className="mb-4">{illustration}</div>}
      <div
        className={mergeClasses(
          "font-semibold tracking-[-0.01em] mb-1",
          compact ? "text-sm" : "text-[16px]",
        )}
      >
        {title}
      </div>
      {description && (
        <p className="text-[12.5px] leading-[1.5] text-fg-muted max-w-[300px]">
          {description}
        </p>
      )}
      {cta && <div className="mt-4">{cta}</div>}
    </div>
  );
}
