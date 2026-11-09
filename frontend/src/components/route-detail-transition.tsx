'use client';

import { createContext, useContext, useState, ViewTransition, type ReactNode } from 'react';
import { FileText } from 'lucide-react';
import { PageHeader } from './page-header';

type DetailSummary = { name: string; title: string; subtitle: string };
const DetailTransitionContext = createContext<{
  summary: DetailSummary | null;
  remember: (summary: DetailSummary) => void;
} | null>(null);

export function DetailTransitionProvider({ children }: { children: ReactNode }) {
  const [summary, remember] = useState<DetailSummary | null>(null);
  return (
    <DetailTransitionContext.Provider value={{ summary, remember }}>
      {children}
    </DetailTransitionContext.Provider>
  );
}

export function useDetailTransition() {
  const context = useContext(DetailTransitionContext);
  if (!context) throw new Error('DetailTransitionProvider is required.');
  return context;
}

export function conversionTransitionName(id: string, extractId?: string | null) {
  return extractId ? `extract-${extractId}` : `conversion-${id}`;
}

// Only display metadata travels with navigation. The document request and loading state stay independent.
export function ConversionDetailHeader({
  id,
  extractId,
  title,
  subtitle,
}: {
  id: string;
  extractId?: string | null;
  title?: string | null;
  subtitle?: string;
}) {
  const { summary } = useDetailTransition();
  const name = conversionTransitionName(id, extractId);
  const handoff = summary?.name === name ? summary : null;
  const displayTitle =
    extractId && handoff ? handoff.title : title || handoff?.title || 'Document bancaire';
  return (
    <ViewTransition key={name} name={name} share="workspace-detail" default="none">
      <PageHeader
        className="conversion-detail-identity"
        eyebrow="Conversion & export"
        title={displayTitle}
        description={subtitle || handoff?.subtitle || 'Conversion et vérification du document'}
        icon={FileText}
      />
    </ViewTransition>
  );
}
