import type { HTMLAttributes, ReactNode } from "react";

interface CardProps extends HTMLAttributes<HTMLDivElement> {
  children: ReactNode;
  interactive?: boolean;
  padded?: boolean;
}

function mergeClasses(...classes: Array<string | undefined | false>): string {
  return classes.filter(Boolean).join(" ");
}

export function Card({
  children,
  interactive = false,
  padded = true,
  className,
  ...props
}: CardProps) {
  return (
    <div
      className={mergeClasses(
        "bg-surface border border-border rounded-md shadow-sm",
        "transition-[box-shadow,transform,border-color] duration-200 ease-[cubic-bezier(0.22,1,0.36,1)]",
        padded && "p-4",
        interactive &&
          "cursor-pointer hover:shadow-md hover:-translate-y-px hover:border-border-strong",
        className,
      )}
      {...props}
    >
      {children}
    </div>
  );
}
