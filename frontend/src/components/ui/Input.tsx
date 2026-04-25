import type { InputHTMLAttributes, ReactNode } from "react";

type InputSize = "sm" | "md" | "lg";

interface InputProps
  extends Omit<InputHTMLAttributes<HTMLInputElement>, "className" | "size"> {
  label?: string;
  error?: string;
  hint?: string;
  leading?: ReactNode;
  trailing?: ReactNode;
  size?: InputSize;
}

const shellSize: Record<InputSize, string> = {
  sm: "h-7 px-2.5",
  md: "h-[34px] px-3",
  lg: "h-10 px-3.5",
};

function mergeClasses(...classes: Array<string | undefined | false>): string {
  return classes.filter(Boolean).join(" ");
}

export function Input({
  label,
  error,
  hint,
  leading,
  trailing,
  size = "md",
  id,
  ...props
}: InputProps) {
  const hasError = Boolean(error);

  return (
    <label htmlFor={id} className="flex w-full flex-col gap-1.5">
      {label && (
        <span className="text-xs font-medium text-fg-muted">{label}</span>
      )}
      <span
        className={mergeClasses(
          "flex items-center gap-2 bg-surface rounded-sm shadow-xs border",
          "transition-[border-color,box-shadow] duration-150 ease-[cubic-bezier(0.22,1,0.36,1)]",
          "focus-within:ring-[3px] focus-within:ring-accent/25",
          hasError
            ? "border-danger focus-within:border-danger focus-within:ring-danger/25"
            : "border-border-strong focus-within:border-accent",
          shellSize[size],
        )}
      >
        {leading && (
          <span className="flex items-center text-fg-subtle shrink-0">
            {leading}
          </span>
        )}
        <input
          id={id}
          className="flex-1 min-w-0 bg-transparent border-0 outline-none text-[13px] text-fg placeholder:text-fg-subtle"
          aria-invalid={hasError}
          aria-describedby={hasError || hint ? `${id}-hint` : undefined}
          {...props}
        />
        {trailing && (
          <span className="flex items-center text-fg-subtle shrink-0">
            {trailing}
          </span>
        )}
      </span>
      {(error || hint) && (
        <span
          id={`${id}-hint`}
          role={hasError ? "alert" : undefined}
          className={mergeClasses(
            "text-[11px]",
            hasError ? "text-danger" : "text-fg-subtle",
          )}
        >
          {error ?? hint}
        </span>
      )}
    </label>
  );
}
