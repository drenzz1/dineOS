type Radius = "none" | "xs" | "sm" | "md" | "lg" | "full";

interface SkeletonProps {
  radius?: Radius;
  className?: string;
  "aria-label"?: string;
}

const radiusClasses: Record<Radius, string> = {
  none: "rounded-none",
  xs: "rounded-xs",
  sm: "rounded-sm",
  md: "rounded-md",
  lg: "rounded-lg",
  full: "rounded-full",
};

function mergeClasses(...classes: Array<string | undefined | false>): string {
  return classes.filter(Boolean).join(" ");
}

export function Skeleton({
  radius = "sm",
  className,
  "aria-label": ariaLabel = "Loading",
}: SkeletonProps) {
  return (
    <span
      role="status"
      aria-label={ariaLabel}
      aria-busy="true"
      className={mergeClasses(
        "block dos-skel-bg animate-shimmer h-3 w-full",
        radiusClasses[radius],
        className,
      )}
    />
  );
}
