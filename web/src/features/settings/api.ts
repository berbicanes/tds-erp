import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, type Paged } from "../../lib/api";
import type { Role } from "../../lib/auth";
import { toApiQuery, type ListState } from "../../lib/listParams";

export interface Employee {
  id: string;
  fullName: string;
  position: string | null;
  isActive: boolean;
  userId: string | null;
  userName: string | null;
  version: number;
}

export interface EmployeeOption {
  id: string;
  fullName: string;
  position: string | null;
}

export interface SaveEmployee {
  fullName: string;
  position: string | null;
  isActive: boolean;
  userId: string | null;
  version: number;
}

export interface User {
  id: string;
  userName: string;
  role: Role;
  isActive: boolean;
  employeeId: string | null;
  employeeName: string | null;
}

export const keys = {
  employees: ["employees"] as const,
  employeeOptions: ["employees", "options"] as const,
  users: ["users"] as const,
};

export function useEmployees(state: ListState) {
  return useQuery({
    queryKey: [...keys.employees, "list", state],
    queryFn: () => api.get<Paged<Employee>>(`/api/employees?${toApiQuery(state)}`),
    placeholderData: keepPreviousData,
  });
}

export function useEmployee(id: string | undefined) {
  return useQuery({
    queryKey: [...keys.employees, id],
    queryFn: () => api.get<Employee>(`/api/employees/${id}`),
    enabled: !!id,
  });
}

/** Active employees for pickers (operator, inspector, responsible person). */
export function useEmployeeOptions() {
  return useQuery({
    queryKey: keys.employeeOptions,
    queryFn: () => api.get<EmployeeOption[]>("/api/employees/options"),
    staleTime: 5 * 60_000,
  });
}

export function useSaveEmployee(id: string | undefined) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: SaveEmployee) =>
      id ? api.put<Employee>(`/api/employees/${id}`, body) : api.post<Employee>("/api/employees", body),
    onSuccess: () => qc.invalidateQueries({ queryKey: keys.employees }),
  });
}

export function useUsers() {
  return useQuery({ queryKey: keys.users, queryFn: () => api.get<User[]>("/api/users") });
}

export function useCreateUser() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: { userName: string; role: Role; password: string }) => api.post<User>("/api/users", body),
    onSuccess: () => qc.invalidateQueries({ queryKey: keys.users }),
  });
}

export function useUpdateUser() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, ...body }: { id: string; role: Role; isActive: boolean }) => api.put<User>(`/api/users/${id}`, body),
    onSuccess: () => qc.invalidateQueries({ queryKey: keys.users }),
  });
}

export function useSetPassword() {
  return useMutation({
    mutationFn: ({ id, password }: { id: string; password: string }) => api.post<void>(`/api/users/${id}/password`, { password }),
  });
}
