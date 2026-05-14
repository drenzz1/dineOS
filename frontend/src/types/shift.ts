export type Priority = "info" | "warning" | "urgent";

export interface ShiftSummary {
  id: string;
  tenantId?: string;
  title: string;
  body: string;
  priority?: Priority;
  author: string;
  createdAt: string;
}

export interface Shift {
  id: string;
  tenantId: string;
  staffMemberId: string;
  staffName: string;
  startTime: string;
  endTime: string;
  notes?: string;
}
