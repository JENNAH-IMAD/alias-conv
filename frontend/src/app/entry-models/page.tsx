'use client';
import { Suspense } from 'react';
import { ClientEntryModels } from '@/components/client-entry-models';
export default function Page() {
  return (
    <Suspense fallback={<p role="status">Chargement des modèles…</p>}>
      <ClientEntryModels />
    </Suspense>
  );
}
