/** Matches FlowDesk.Domain CustomerStatus. Codes stay English; labels are mapped for display. */
export const CUSTOMER_STATUSES = ["Active", "Inactive"] as const;
export type CustomerStatus = (typeof CUSTOMER_STATUSES)[number];

/** Matches FlowDesk.Application CustomerSort. */
export const CUSTOMER_SORTS = [
  "RecentlyUpdated",
  "NameAscending",
  "NameDescending",
  "RecentlyCreated",
] as const;
export type CustomerSort = (typeof CUSTOMER_SORTS)[number];

export type CustomerListItem = {
  id: string;
  name: string;
  email: string | null;
  phone: string | null;
  company: string | null;
  status: CustomerStatus;
  isArchived: boolean;
  createdAt: string;
  updatedAt: string;
};

export type CustomerDetail = CustomerListItem & {
  notes: string | null;
};

export type CustomerInput = {
  name: string;
  email: string | null;
  phone: string | null;
  company: string | null;
  status: CustomerStatus;
  notes: string | null;
};

export type CustomerFilters = {
  search: string;
  status: CustomerStatus | null;
  includeArchived: boolean;
  sort: CustomerSort;
  page: number;
};

export type PagedResponse<TItem> = {
  items: TItem[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
};
