import { useQuery } from "@tanstack/react-query";
import { api } from "../../lib/api";
import type { NoteDto } from "./notesApi";
import type { WorkOrder } from "../workOrders/api";
import type { ItemStock } from "../stock/stockApi";

export interface DashboardKpi {
  activeOrders: number;
  overdueOrders: number;
  dueIn7Days: number;
  urgentOrders: number;
  goodToday: number;
  entriesToday: number;
  itemsBelowMinimum: number;
}

export interface TodayEntry {
  id: string;
  number: string;
  operatorName: string;
  workOrderId: string;
  workOrderNumber: string;
  customer: string;
  laserCutQty: number;
  pressedQty: number;
  goodQty: number;
  scrapQty: number;
  laborHours: number;
}

export interface ChartDay {
  date: string;
  laserCutQty: number;
  pressedQty: number;
  goodQty: number;
  scrapQty: number;
}

export type WarningKind =
  | "LotKarantin" | "LotNaKontroli" | "OrderBlocked" | "SupplierEvaluation"
  | "Calibration" | "CalibrationMissing" | "CalibrationFailed" | "Stocktake";

export interface DashboardWarning {
  kind: WarningKind;
  severity: "red" | "amber";
  entityId: string;
  title: string;
  detail: string | null;
  itemName: string | null;
  itemGrade: string | null;
  itemThicknessMm: number | null;
  date: string | null;
}

export interface Dashboard {
  generatedAt: string;
  today: string;
  kpi: DashboardKpi;
  todayEntries: TodayEntry[];
  todayTotals: { laserCutQty: number; pressedQty: number; goodQty: number; scrapQty: number; laborHours: number };
  chart: ChartDay[];
  warnings: DashboardWarning[];
  ordersByDue: { items: WorkOrder[]; total: number };
  stock: { items: ItemStock[]; total: number };
  notes: NoteDto[];
}

export const dashboardKey = ["dashboard"] as const;

/** UI §3: one call; refresh every 60 s and on window focus. */
export function useDashboard() {
  return useQuery({
    queryKey: dashboardKey,
    queryFn: () => api.get<Dashboard>("/api/dashboard"),
    refetchInterval: 60_000,
    refetchOnWindowFocus: true,
    staleTime: 0,
  });
}
