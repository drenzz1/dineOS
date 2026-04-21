import { Card } from "@/components/ui/Card";
import type { ActivityEvent } from "@/hooks/useAdminAnalytics";

function formatRelative(iso: string): string {
  const diff = Math.floor((Date.now() - new Date(iso).getTime()) / 60_000);
  if (diff < 1) return "just now";
  if (diff < 60) return `${diff}m ago`;
  const hours = Math.floor(diff / 60);
  if (hours < 24) return `${hours}h ago`;
  return `${Math.floor(hours / 24)}d ago`;
}

function FeedSkeleton() {
  return (
    <Card className="animate-pulse space-y-4">
      <div className="h-5 w-28 rounded bg-zinc-200" />
      {Array.from({ length: 5 }).map((_, i) => (
        <div key={i} className="flex items-start gap-3">
          <div className="mt-0.5 h-2 w-2 shrink-0 rounded-full bg-zinc-200" />
          <div className="flex-1 space-y-1">
            <div className="h-3.5 w-3/4 rounded bg-zinc-200" />
            <div className="h-3 w-16 rounded bg-zinc-100" />
          </div>
        </div>
      ))}
    </Card>
  );
}

interface ActivityFeedProps {
  events: ActivityEvent[] | null;
  isLoading: boolean;
}

export default function ActivityFeed({ events, isLoading }: ActivityFeedProps) {
  if (isLoading || !events) return <FeedSkeleton />;

  return (
    <Card className="space-y-4">
      <h2 className="text-sm font-semibold text-zinc-900">Recent Activity</h2>
      <ul className="space-y-3">
        {events.map((evt) => (
          <li key={evt.id} className="flex items-start gap-3">
            <span className="mt-1.5 h-2 w-2 shrink-0 rounded-full bg-indigo-500" />
            <div>
              <p className="text-sm text-zinc-800">{evt.description}</p>
              <p className="mt-0.5 text-xs text-zinc-400">
                {formatRelative(evt.timestamp)}
              </p>
            </div>
          </li>
        ))}
      </ul>
    </Card>
  );
}
