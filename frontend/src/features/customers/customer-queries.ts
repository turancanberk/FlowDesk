"use client";

import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  archiveCustomer,
  createCustomer,
  getCustomer,
  listCustomers,
  restoreCustomer,
  updateCustomer,
} from "./customer-api";
import type { CustomerFilters, CustomerInput } from "./customer-types";

export const customerKeys = {
  all: (slug: string) => ["customers", slug] as const,
  list: (slug: string, filters: CustomerFilters) =>
    [...customerKeys.all(slug), "list", filters] as const,
  detail: (slug: string, customerId: string) =>
    [...customerKeys.all(slug), "detail", customerId] as const,
};

export function useCustomers(slug: string, filters: CustomerFilters) {
  return useQuery({
    queryKey: customerKeys.list(slug, filters),
    queryFn: ({ signal }) => listCustomers(slug, filters, signal),
    /*
      Keeps the previous page on screen while the next one loads. Without it,
      typing in the search box or changing pages empties the table on every
      keystroke and the layout jumps.
    */
    placeholderData: keepPreviousData,
  });
}

export function useCustomer(slug: string, customerId: string) {
  return useQuery({
    queryKey: customerKeys.detail(slug, customerId),
    queryFn: ({ signal }) => getCustomer(slug, customerId, signal),
    retry: false,
  });
}

export function useCreateCustomer(slug: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (input: CustomerInput) => createCustomer(slug, input),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: customerKeys.all(slug) });
    },
  });
}

export function useUpdateCustomer(slug: string, customerId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (input: CustomerInput) => updateCustomer(slug, customerId, input),
    onSuccess: (customer) => {
      queryClient.setQueryData(customerKeys.detail(slug, customerId), customer);
      void queryClient.invalidateQueries({ queryKey: customerKeys.all(slug) });
    },
  });
}

export function useArchiveCustomer(slug: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (customerId: string) => archiveCustomer(slug, customerId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: customerKeys.all(slug) });
    },
  });
}

export function useRestoreCustomer(slug: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (customerId: string) => restoreCustomer(slug, customerId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: customerKeys.all(slug) });
    },
  });
}
