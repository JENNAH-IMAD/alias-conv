'use client';
import { useCallback, useEffect, useState } from 'react';
import { all, request } from './api';
export function useApi<T>(path: string | null, keepPreviousData = false) {
  const [result, setResult] = useState<{
    path: string;
    version: number;
    data: T | null;
    error: string;
  } | null>(null);
  const [version, setVersion] = useState(0);
  const reload = useCallback(() => setVersion((v) => v + 1), []);
  useEffect(() => {
    if (path === null) return;
    let active = true;
    const controller = new AbortController();
    request<T>(path, { signal: controller.signal })
      .then((data) => {
        if (active) setResult({ path, version, data, error: '' });
      })
      .catch((e) => {
        if (active) setResult({ path, version, data: null, error: e.message });
      });
    return () => {
      active = false;
      controller.abort();
    };
  }, [path, version]);
  const current = result?.path === path && result.version === version;
  return {
    data: current || (keepPreviousData && result?.path === path) ? (result?.data ?? null) : null,
    error: current ? result.error : '',
    loading: path !== null && !current,
    reload,
  };
}
export function useAll<T>(path: string) {
  const [result, setResult] = useState<{ version: number; data: T[] | null; error: string } | null>(
    null,
  );
  const [version, setVersion] = useState(0);
  const reload = useCallback(() => setVersion((v) => v + 1), []);
  useEffect(() => {
    const controller = new AbortController();
    all<T>(path, controller.signal)
      .then((data) => {
        if (!controller.signal.aborted) setResult({ version, data, error: '' });
      })
      .catch((e) => {
        if (!controller.signal.aborted) setResult({ version, data: null, error: e.message });
      });
    return () => controller.abort();
  }, [path, version]);
  return {
    data: result?.version === version ? result.data : null,
    error: result?.version === version ? result.error : '',
    loading: result?.version !== version,
    reload,
  };
}
