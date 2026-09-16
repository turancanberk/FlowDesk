"use client";

import * as React from "react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { ApiError } from "@/lib/api/api-error";

/*
  Server-state provider.

  The client is created inside a state initialiser rather than at module scope
  so that each browser session gets its own cache. A module-level client would
  be shared across requests during server rendering and leak one user's data
  into another's response.
*/
export function QueryProvider({ children }: { children: React.ReactNode }) {
  const [queryClient] = React.useState(
    () =>
      new QueryClient({
        defaultOptions: {
          queries: {
            staleTime: 30_000,
            refetchOnWindowFocus: false,
            retry: (failureCount, error) => {
              // Retrying a rejected request neither fixes the permission nor
              // finds the missing record; it just delays the message.
              if (error instanceof ApiError && error.status < 500) {
                return false;
              }

              return failureCount < 2;
            },
          },
          mutations: {
            // Mutations are never retried automatically. A create that appears
            // to fail may have succeeded, and repeating it would duplicate the
            // record.
            retry: false,
          },
        },
      }),
  );

  return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
}
