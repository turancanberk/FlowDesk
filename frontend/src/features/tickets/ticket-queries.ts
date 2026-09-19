"use client";

import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import type { TicketStatus } from "@/types/domain";
import {
  addTicketComment,
  assignTicket,
  changeTicketStatus,
  createTicket,
  deleteAttachment,
  deleteTicket,
  getTicket,
  listAttachments,
  listTicketComments,
  listTickets,
  updateTicket,
  uploadAttachment,
} from "./ticket-api";
import type {
  CreateTicketInput,
  TicketDetail,
  TicketFilters,
  UpdateTicketInput,
} from "./ticket-types";

export const ticketKeys = {
  all: (slug: string) => ["tickets", slug] as const,
  list: (slug: string, filters: TicketFilters) =>
    [...ticketKeys.all(slug), "list", filters] as const,
  detail: (slug: string, ticketId: string) =>
    [...ticketKeys.all(slug), "detail", ticketId] as const,
  comments: (slug: string, ticketId: string) =>
    [...ticketKeys.all(slug), "comments", ticketId] as const,
  attachments: (slug: string, ticketId: string) =>
    [...ticketKeys.all(slug), "attachments", ticketId] as const,
};

export function useTickets(slug: string, filters: TicketFilters) {
  return useQuery({
    queryKey: ticketKeys.list(slug, filters),
    queryFn: ({ signal }) => listTickets(slug, filters, signal),
    // Keeps the current page on screen while the next one loads, so the table
    // does not empty on every keystroke.
    placeholderData: keepPreviousData,
  });
}

export function useTicket(slug: string, ticketId: string) {
  return useQuery({
    queryKey: ticketKeys.detail(slug, ticketId),
    queryFn: ({ signal }) => getTicket(slug, ticketId, signal),
    retry: false,
  });
}

export function useTicketComments(slug: string, ticketId: string) {
  return useQuery({
    queryKey: ticketKeys.comments(slug, ticketId),
    queryFn: ({ signal }) => listTicketComments(slug, ticketId, signal),
  });
}

export function useTicketAttachments(slug: string, ticketId: string) {
  return useQuery({
    queryKey: ticketKeys.attachments(slug, ticketId),
    queryFn: ({ signal }) => listAttachments(slug, ticketId, signal),
  });
}

export function useUploadAttachment(slug: string, ticketId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (file: File) => uploadAttachment(slug, ticketId, file),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ticketKeys.attachments(slug, ticketId) });
    },
  });
}

export function useDeleteAttachment(slug: string, ticketId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (attachmentId: string) => deleteAttachment(slug, ticketId, attachmentId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ticketKeys.attachments(slug, ticketId) });
    },
  });
}

export function useCreateTicket(slug: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (input: CreateTicketInput) => createTicket(slug, input),
    onSuccess: (ticket) => {
      queryClient.setQueryData(ticketKeys.detail(slug, ticket.id), ticket);
      void queryClient.invalidateQueries({ queryKey: ticketKeys.all(slug) });
    },
  });
}

export function useUpdateTicket(slug: string, ticketId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (input: UpdateTicketInput) => updateTicket(slug, ticketId, input),
    onSuccess: (ticket) => writeBack(queryClient, slug, ticket),
  });
}

export function useChangeTicketStatus(slug: string, ticketId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (status: TicketStatus) => changeTicketStatus(slug, ticketId, status),
    onSuccess: (ticket) => writeBack(queryClient, slug, ticket),
  });
}

export function useAssignTicket(slug: string, ticketId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (assignedUserId: string | null) => assignTicket(slug, ticketId, assignedUserId),
    onSuccess: (ticket) => writeBack(queryClient, slug, ticket),
  });
}

export function useDeleteTicket(slug: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (ticketId: string) => deleteTicket(slug, ticketId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ticketKeys.all(slug) });
    },
  });
}

export function useAddTicketComment(slug: string, ticketId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (body: string) => addTicketComment(slug, ticketId, body),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ticketKeys.comments(slug, ticketId) });
    },
  });
}

/**
 * Stores the server's copy of the ticket and refreshes the lists.
 *
 * Writing the response straight into the detail cache matters more here than
 * elsewhere: it carries the new row version, and an edit form left holding the
 * old one would report a conflict against the user's own previous save.
 */
function writeBack(
  queryClient: ReturnType<typeof useQueryClient>,
  slug: string,
  ticket: TicketDetail,
) {
  queryClient.setQueryData(ticketKeys.detail(slug, ticket.id), ticket);
  void queryClient.invalidateQueries({ queryKey: ticketKeys.all(slug) });
}
