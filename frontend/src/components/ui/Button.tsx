import type { ButtonHTMLAttributes, ReactNode } from "react";

type ButtonVariant = "primary" | "secondary" | "danger" | "ghost";
type ButtonSize = "sm" | "md" | "lg";

interface ButtonProps extends Omit<ButtonHTMLAttributes<HTMLButtonElement>, "children"> {
  children: ReactNode;
  variant?: ButtonVariant;
  size?: ButtonSize;
  isLoading?: boolean;
  leading?: ReactNode;
  trailing?: ReactNode;
  block?: boolean;
}

const variantClasses: Record<ButtonVariant, string> = {
  primary:
    "bg-accent text-accent-fg hover:bg-accent-hover shadow-xs ring-inset ring-white/10",
  secondary:
    "bg-surface text-fg border border-border-strong hover:bg-surface-2 shadow-xs",
  danger:
    "bg-danger text-warm-0 hover:bg-[oklch(0.52_0.22_25)] shadow-xs",
  ghost:
    "bg-transparent text-fg-muted hover:bg-surface-2 hover:text-fg",
};

const sizeClasses: Record<ButtonSize, string> = {
  sm: "h-7 px-2.5 text-xs gap-1.5 rounded-sm",
  md: "h-[34px] px-3 text-[13px] gap-1.5 rounded-md",
  lg: "h-10 px-4 text-sm gap-2 rounded-md",
};

function mergeClasses(...classes: Array<string | undefined | false>): string {
  return classes.filter(Boolean).join(" ");
}

export function Button({
  children,
  variant = "primary",
  size = "md",
  isLoading = false,
  leading,
  trailing,
  block = false,
  disabled,
  className,
  type = "button",
  ...props
}: ButtonProps) {
  const finalDisabled = (disabled ?? false) || isLoading;

  return (
    <button
      type={type}
      disabled={finalDisabled}
      className={mergeClasses(
        "inline-flex items-center justify-center font-[550] tracking-[-0.005em]",
        "transition-[background-color,transform,box-shadow,color,border-color] duration-150 ease-[cubic-bezier(0.22,1,0.36,1)]",
        "hover:-translate-y-[0.5px] active:translate-y-0",
        "focus-visible:outline-none",
        "disabled:cursor-not-allowed disabled:opacity-50 disabled:hover:translate-y-0",
        sizeClasses[size],
        variantClasses[variant],
        block && "w-full",
        className,
      )}
      {...props}
    >
      {isLoading ? (
        <span
          className="inline-block h-3.5 w-3.5 rounded-full border-[1.5px] border-current border-t-transparent animate-spin-fast"
          aria-hidden="true"
        />
      ) : (
        leading
      )}
      <span>{children}</span>
      {!isLoading && trailing}
    </button>
  );
}
