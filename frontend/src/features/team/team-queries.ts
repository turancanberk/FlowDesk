"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import type { MembershipRole } from "@/features/workspaces/workspace-types";
import { workspaceKeys } from "@/features/workspaces/workspace-queries";
import {
  acceptInvitation,
  changeMemberRole,
  inviteMember,
  listInvitations,
  listMembers,
  removeMember,
  revokeInvitation,
} from "./team-api";

export const teamKeys = {
  members: (slug: string) => ["team", slug, "members"] as const,
  invitations: (slug: string) => ["team", slug, "invitations"] as const,
};

export function useMembers(slug: string) {
  return useQuery({
    queryKey: teamKeys.members(slug),
    queryFn: ({ signal }) => listMembers(slug, signal),
  });
}

export function useInvitations(slug: string) {
  return useQuery({
    queryKey: teamKeys.invitations(slug),
    queryFn: ({ signal }) => listInvitations(slug, signal),
  });
}

export function useInviteMember(slug: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (input: { email: string; role: MembershipRole }) => inviteMember(slug, input),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: teamKeys.invitations(slug) });
    },
  });
}

export function useRevokeInvitation(slug: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (invitationId: string) => revokeInvitation(slug, invitationId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: teamKeys.invitations(slug) });
    },
  });
}

export function useChangeMemberRole(slug: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (input: { userId: string; role: MembershipRole }) =>
      changeMemberRole(slug, input.userId, input.role),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: teamKeys.members(slug) });
      // The caller's own role may have changed, and the sidebar and the
      // workspace switcher both display it.
      void queryClient.invalidateQueries({ queryKey: workspaceKeys.all });
    },
  });
}

export function useRemoveMember(slug: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (userId: string) => removeMember(slug, userId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: teamKeys.members(slug) });
      void queryClient.invalidateQueries({ queryKey: workspaceKeys.all });
    },
  });
}

export function useAcceptInvitation() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: acceptInvitation,
    onSuccess: () => {
      // The user now belongs to one more workspace.
      void queryClient.invalidateQueries({ queryKey: workspaceKeys.all });
    },
  });
}
