'use client';
import { Button } from './button';
import { ChevronLeft, ChevronRight } from 'lucide-react';
export function Pagination({
  page,
  total,
  onChange,
}: {
  page: number;
  total: number;
  onChange: (n: number) => void;
}) {
  return (
    <nav aria-label="Pagination" className="pagination">
      <span>
        {total} élément{total > 1 ? 's' : ''} · Page {page} sur {Math.max(1, Math.ceil(total / 20))}
      </span>
      <div className="flex gap-2">
        <Button variant="outline" disabled={page === 1} onClick={() => onChange(page - 1)}>
          <ChevronLeft size={14} /> Précédent
        </Button>
        <Button variant="outline" disabled={page * 20 >= total} onClick={() => onChange(page + 1)}>
          Suivant <ChevronRight size={14} />
        </Button>
      </div>
    </nav>
  );
}
