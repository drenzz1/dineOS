export type Priority = "info" | "warning" | "urgent";

export interface ShiftSummary {
  id: string;
  title: string;
  body: string;
  priority?: Priority;
  author: string;
  createdAt: string;
}
