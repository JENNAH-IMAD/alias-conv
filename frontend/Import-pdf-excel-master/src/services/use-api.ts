'use client';
import { useCallback, useEffect, useState } from 'react';
import { request } from './api';
export function useApi<T>(path: string, keepPreviousData = false) {
  const [result, setResult] = useState<{ path: string; version: number; data: T | null; error: string } | null>(null);
  const [version, setVersion] = useState(0);
  const reload = useCallback(() => setVersion(v => v + 1), []);
  useEffect(() => {
    let active = true;
    const controller = new AbortController();
    request<T>(path, { signal: controller.signal }).then(data => { if (active) setResult({path, version, data, error: ''}); }).catch(e => { if (active) setResult({path, version, data: null, error: e.message}); });
    return () => { active = false; controller.abort(); };
  }, [path, version]);
  const current = result?.path === path && result.version === version;
  return { data: current || (keepPreviousData && result?.path === path) ? result?.data ?? null : null, error: current ? result.error : '', loading: !current, reload };
}
