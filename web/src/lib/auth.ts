import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ApiError, api } from "./api";

export type Role = "Admin" | "Kontrolor" | "Operater";

export interface Me {
  id: string;
  userName: string;
  roles: Role[];
}

export const meQueryKey = ["me"] as const;

/** Current user, or null when not signed in. */
export function useMe() {
  return useQuery({
    queryKey: meQueryKey,
    queryFn: async (): Promise<Me | null> => {
      try {
        return await api.get<Me>("/api/me");
      } catch (e) {
        if (e instanceof ApiError && e.status === 401) return null;
        throw e;
      }
    },
    staleTime: 5 * 60_000,
  });
}

export function hasRole(me: Me | null | undefined, ...roles: Role[]): boolean {
  return !!me && me.roles.some((r) => roles.includes(r));
}

/** Redirect target after login: only paths inside this app (no open redirect). */
export function safeNext(next: string | null): string {
  return next && next.startsWith("/") && !next.startsWith("//") && !next.startsWith("/\\") ? next : "/";
}

export function useLogin() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: { userName: string; password: string }) => api.post<Me>("/api/auth/login", input),
    onSuccess: (me) => queryClient.setQueryData(meQueryKey, me),
  });
}

export function useLogout() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => api.post<void>("/api/auth/logout"),
    onSuccess: () => {
      // Keep the "me" query (RequireAuth observes it) and drop all other cached server data.
      queryClient.setQueryData(meQueryKey, null);
      queryClient.removeQueries({ predicate: (q) => q.queryKey[0] !== meQueryKey[0] });
    },
  });
}
