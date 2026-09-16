import type { TicketPriority, TicketStatus } from "@/types/domain";

export type TicketStatusCount = {
  status: TicketStatus;
  count: number;
};

export type RecentTicket = {
  id: string;
  number: number;
  subject: string;
  customerName: string;
  status: TicketStatus;
  priority: TicketPriority;
  updatedAt: string;
};

export type UpcomingTask = {
  id: string;
  title: string;
  dueAt: string;
  isOverdue: boolean;
  assignedUserDisplayName: string | null;
};

export type DashboardSummary = {
  customerCount: number;
  /** Everything unfinished, not the Open status alone. */
  openTicketCount: number;
  unassignedTicketCount: number;
  overdueTaskCount: number;
  dueSoonTaskCount: number;
  memberCount: number;
  ticketsByStatus: TicketStatusCount[];
  recentTickets: RecentTicket[];
  upcomingTasks: UpcomingTask[];
  /** When the figures were read, so the screen can say how fresh they are. */
  generatedAt: string;
};
