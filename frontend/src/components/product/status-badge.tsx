import { cva, type VariantProps } from "class-variance-authority";
import { cn } from "cn";
import {
  customerStatusLabels,
  membershipRoleLabels,
  taskStatusLabels,
  ticketPriorityLabels,
  ticketStatusLabels,
  type Tone,
} from "@/lib/domain-labels";
import type {
  CustomerStatus,
  MembershipRole,
  TaskStatus,
  TicketPriority,
  TicketStatus,
} from "@/types/domain";

/*
  Durum göstergesi.

  Genel `Badge` bileşeni yerine ayrı bir bileşen var, çünkü buradaki iş domain
  kodunu Türkçe etikete ve doğru tona çevirmek. Bu eşlemeyi her çağrı yerinde
  tekrarlamak, bir durum eklendiğinde arayüzün sessizce eksik kalmasına yol
  açardı.

  Rozet ince kenarlıklı ve açık zeminlidir; dolgu rozet tabloda gürültü yapar.
*/

const statusBadgeVariants = cva(
  "inline-flex h-5 w-fit shrink-0 items-center gap-1 rounded-sm border px-1.5 text-xs font-medium whitespace-nowrap",
  {
    variants: {
      tone: {
        neutral: "border-border bg-muted text-muted-foreground",
        info: "border-info-border bg-info-surface text-info",
        accent: "border-accent-border bg-accent-surface text-accent-text",
        warning: "border-warning-border bg-warning-surface text-warning",
        success: "border-success-border bg-success-surface text-success",
        danger: "border-danger-border bg-danger-surface text-danger",
      } satisfies Record<Tone, string>,
    },
    defaultVariants: { tone: "neutral" },
  },
);

type StatusBadgeProps = React.ComponentProps<"span"> & VariantProps<typeof statusBadgeVariants>;

function StatusBadge({ className, tone, children, ...props }: StatusBadgeProps) {
  return (
    <span
      data-slot="status-badge"
      className={cn(statusBadgeVariants({ tone }), className)}
      {...props}
    >
      {children}
    </span>
  );
}

function TicketStatusBadge({ status, className }: { status: TicketStatus; className?: string }) {
  const { label, tone } = ticketStatusLabels[status];
  return (
    <StatusBadge tone={tone} className={className}>
      {label}
    </StatusBadge>
  );
}

function TicketPriorityBadge({
  priority,
  className,
}: {
  priority: TicketPriority;
  className?: string;
}) {
  const { label, tone } = ticketPriorityLabels[priority];
  return (
    <StatusBadge tone={tone} className={className}>
      {label}
    </StatusBadge>
  );
}

function TaskStatusBadge({ status, className }: { status: TaskStatus; className?: string }) {
  const { label, tone } = taskStatusLabels[status];
  return (
    <StatusBadge tone={tone} className={className}>
      {label}
    </StatusBadge>
  );
}

function CustomerStatusBadge({
  status,
  className,
}: {
  status: CustomerStatus;
  className?: string;
}) {
  const { label, tone } = customerStatusLabels[status];
  return (
    <StatusBadge tone={tone} className={className}>
      {label}
    </StatusBadge>
  );
}

function MembershipRoleBadge({ role, className }: { role: MembershipRole; className?: string }) {
  const { label, tone } = membershipRoleLabels[role];
  return (
    <StatusBadge tone={tone} className={className}>
      {label}
    </StatusBadge>
  );
}

export {
  CustomerStatusBadge,
  MembershipRoleBadge,
  StatusBadge,
  statusBadgeVariants,
  TaskStatusBadge,
  TicketPriorityBadge,
  TicketStatusBadge,
};
