import type { HTMLAttributes, ReactNode } from "react";

interface CardProps extends HTMLAttributes<HTMLDivElement> {
  children: ReactNode;
}

function mergeClasses(...classes: Array<string | undefined | false>): string {
  return classes.filter(Boolean).join(" ");
}

export function Card({ children, className, ...props }: CardProps) {
  return (
    <div
      className={mergeClasses("rounded-lg bg-white p-4 shadow-sm", className)}
      {...props}
    >
      {children}
    </div>
  );
}
