import { useCallback, useMemo } from "react";
import { useSearchParams } from "react-router";
import { readListState, writeListState, type ListState } from "../lib/listParams";

/** List state (page, sort, search, filters) read from and written to the URL. */
export function useListState() {
  const [params, setParams] = useSearchParams();
  const state = useMemo(() => readListState(params), [params]);
  const setState = useCallback(
    (next: ListState) => setParams(writeListState(next), { replace: false }),
    [setParams],
  );
  return [state, setState] as const;
}
