"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { login, logout, register, restoreSession } from "./auth-api";
import type { CurrentUser } from "./auth-types";

/*
  Server state for authentication lives in TanStack Query rather than a
  bespoke context. The session is server state: it can be fetched, it can go
  stale, and it has to be invalidated on sign-out. Reimplementing that in a
  reducer would duplicate what the query cache already does well.
*/

export const currentUserQueryKey = ["auth", "current-user"] as const;

/**
 * The signed-in user, or `null` when there is no valid session.
 *
 * Reads through `restoreSession`, so a page load transparently exchanges the
 * refresh cookie for a new access token before answering.
 */
export function useCurrentUser() {
  return useQuery<CurrentUser | null>({
    queryKey: currentUserQueryKey,
    queryFn: () => restoreSession(),
    // A missing session is a normal answer, not a failure to retry.
    retry: false,
    staleTime: 5 * 60 * 1000,
    refetchOnWindowFocus: false,
  });
}

export function useLogin() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: login,
    onSuccess: (user) => {
      queryClient.setQueryData(currentUserQueryKey, user);
    },
  });
}

export function useRegister() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: register,
    onSuccess: (user) => {
      queryClient.setQueryData(currentUserQueryKey, user);
    },
  });
}

export function useLogout() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: logout,
    onSettled: () => {
      // Drop the whole cache, not just the session. Anything fetched while
      // signed in belonged to that user and must not be visible to the next
      // one on this device.
      queryClient.clear();
    },
  });
}
