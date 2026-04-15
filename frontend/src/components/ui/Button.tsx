import type { ButtonHTMLAttributes, ReactNode } from "react";

type ButtonVariant = "primary" | "secondary" | "danger" | "ghost";
type ButtonSize = "sm" | "md" | "lg";

interface ButtonProps extends Omit<ButtonHTMLAttributes<HTMLButtonElement>, "children"> {
  children: ReactNode;
  variant?: ButtonVariant;
  size?: ButtonSize;
  isLoading?: boolean;
}

const variantClasses: Record<ButtonVariant, string> = {
  primary: "bg-blue-600 text-white hover:bg-blue-700 focus-visible:ring-blue-500",
  secondary: "bg-gray-200 text-gray-900 hover:bg-gray-300 focus-visible:ring-gray-400",
  danger: "bg-red-600 text-white hover:bg-red-700 focus-visible:ring-red-500",
  ghost: "bg-transparent text-gray-900 hover:bg-gray-100 focus-visible:ring-gray-300",
};

const sizeClasses: Record<ButtonSize, string> = {
  sm: "h-8 px-3 text-sm",
  md: "h-10 px-4 text-sm",
  lg: "h-12 px-6 text-base",
};

function mergeClasses(...classes: Array<string | undefined | false>): string {
  return classes.filter(Boolean).join(" ");
}

export function Button({
  children,
  variant = "primary",
  size = "md",
  isLoading = false,
  disabled,
  className,
  ...props
}: ButtonProps) {
  const isDisabled = disabled ?? false;
  const finalDisabled = isDisabled || isLoading;

  return (
    <button
      type="button"
      disabled={finalDisabled}
      className={mergeClasses(
        "inline-flex items-center justify-center rounded-md font-medium transition-colors",
        "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-offset-2",
        "disabled:cursor-not-allowed disabled:opacity-50",
        variantClasses[variant],
        sizeClasses[size],
        className
      )}
      {...props}
    >
      {isLoading ? (
        <>
          <span
            className="mr-2 inline-block h-4 w-4 animate-spin rounded-full border-2 border-current border-t-transparent"
            aria-hidden="true"
          />
          <span>Loading...</span>
        </>
      ) : (
        children
      )}
    </button>
  );
}
