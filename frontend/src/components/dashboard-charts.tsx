'use client';
import { useId, useState, useSyncExternalStore } from 'react';
import type { Dashboard } from '@/services/api';

export const dashboardStatuses: Record<string, { label: string; color: string }> = {
  EXPORTED: { label: 'Exportés', color: '#3478f6' },
  VALIDATED: { label: 'Validés', color: '#19a68e' },
  NEEDS_REVIEW: { label: 'À vérifier', color: '#e8a23b' },
  FAILED: { label: 'En erreur', color: '#db6483' },
};
export const statusInfo = (status: string) =>
  dashboardStatuses[status] ?? { label: status, color: '#8996ae' };
const shortDate = (date: string) =>
  new Date(`${date}T12:00:00Z`).toLocaleDateString('fr-FR', { day: 'numeric', month: 'short' });
const subscribeToChartWidth = (callback: () => void) => {
  const query = window.matchMedia('(max-width: 650px)');
  query.addEventListener('change', callback);
  return () => query.removeEventListener('change', callback);
};

export function ActivityChart({ data }: { data: Dashboard['activity'] }) {
  const gradient = useId().replace(/:/g, '');
  const [active, setActive] = useState<number | null>(null);
  const compact = useSyncExternalStore(
    subscribeToChartWidth,
    () => window.matchMedia('(max-width: 650px)').matches,
    () => false,
  );
  const width = compact ? 400 : 750;
  const left = 38,
    right = width - 24,
    top = 22,
    bottom = 200;
  const max = Math.max(
    4,
    Math.ceil(Math.max(...data.flatMap((day) => [day.imports, day.exports]), 0) / 4) * 4,
  );
  const x = (index: number) => left + (index * (right - left)) / Math.max(1, data.length - 1);
  const y = (value: number) => bottom - (value * (bottom - top)) / max;
  const path = (field: 'imports' | 'exports') =>
    data.map((day, index) => `${index ? 'L' : 'M'}${x(index)},${y(day[field])}`).join(' ');
  const hasActivity = data.some((day) => day.imports || day.exports);
  const selected = active === null ? null : data[active];
  return (
    <div className="overview-chart-wrap">
      <div className="overview-chart-readout" aria-live="polite">
        {selected ? (
          <>
            <strong>{shortDate(selected.date)}</strong>
            <span>
              <i className="overview-dot blue" />
              {selected.imports} import(s)
            </span>
            <span>
              <i className="overview-dot teal" />
              {selected.exports} export(s)
            </span>
          </>
        ) : (
          <span>Survolez le graphique pour consulter une journée.</span>
        )}
      </div>
      <svg
        className="overview-activity-svg"
        viewBox={`0 0 ${width} 235`}
        role="group"
        aria-label="Graphique des imports et exports par jour"
        onMouseLeave={() => setActive(null)}
      >
        <defs>
          <linearGradient id={gradient} x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" stopColor="#3478f6" stopOpacity=".22" />
            <stop offset="100%" stopColor="#3478f6" stopOpacity=".01" />
          </linearGradient>
        </defs>
        {[0, 1, 2, 3, 4].map((tick) => (
          <g key={tick}>
            <line
              className="overview-chart-grid"
              x1={left}
              x2={right}
              y1={y((max * tick) / 4)}
              y2={y((max * tick) / 4)}
            />
            <text
              className="overview-chart-label"
              x={left - 12}
              y={y((max * tick) / 4) + 4}
              textAnchor="end"
            >
              {(max * tick) / 4}
            </text>
          </g>
        ))}
        {hasActivity && (
          <>
            <path
              d={`${path('imports')} L${x(data.length - 1)},${bottom} L${left},${bottom} Z`}
              fill={`url(#${gradient})`}
            />
            <path
              d={path('imports')}
              stroke="#3478f6"
              strokeWidth="3"
              fill="none"
              strokeLinejoin="round"
            />
            <path
              d={path('exports')}
              stroke="#19a68e"
              strokeWidth="2.5"
              strokeDasharray="6 4"
              fill="none"
              strokeLinejoin="round"
            />
          </>
        )}
        {data
          .filter((_, index) =>
            [
              0,
              Math.floor((data.length - 1) / 4),
              Math.floor((data.length - 1) / 2),
              Math.floor((3 * (data.length - 1)) / 4),
              data.length - 1,
            ].includes(index),
          )
          .map((day) => (
            <text
              className="overview-chart-label"
              key={day.date}
              x={x(data.indexOf(day))}
              y="226"
              textAnchor="middle"
            >
              {shortDate(day.date)}
            </text>
          ))}
        {!hasActivity && (
          <text className="overview-chart-empty" x={(left + right) / 2} y="110" textAnchor="middle">
            Aucune activité sur cette période
          </text>
        )}
        {selected && active !== null && (
          <g>
            <line
              x1={x(active)}
              x2={x(active)}
              y1={top}
              y2={bottom}
              stroke="var(--muted-foreground)"
              strokeDasharray="3 4"
              opacity=".5"
            />
            <circle
              cx={x(active)}
              cy={y(selected.imports)}
              r="5"
              fill="#3478f6"
              stroke="var(--surface)"
              strokeWidth="2"
            />
            <circle
              cx={x(active)}
              cy={y(selected.exports)}
              r="4"
              fill="#19a68e"
              stroke="var(--surface)"
              strokeWidth="2"
            />
          </g>
        )}
        {data.map((day, index) => (
          <rect
            key={day.date}
            className="overview-chart-hit"
            x={x(index) - (right - left) / Math.max(1, data.length - 1) / 2}
            y={top}
            width={(right - left) / Math.max(1, data.length - 1)}
            height={bottom - top}
            fill="transparent"
            tabIndex={0}
            role="button"
            aria-label={`${shortDate(day.date)} : ${day.imports} imports, ${day.exports} exports`}
            onMouseEnter={() => setActive(index)}
            onFocus={() => setActive(index)}
            onBlur={() => setActive(null)}
            onClick={() => setActive(index)}
            onKeyDown={(event) => {
              if (event.key === 'Enter' || event.key === ' ') {
                event.preventDefault();
                setActive(index);
              }
            }}
          />
        ))}
      </svg>
      <div className="overview-chart-legend">
        <span>
          <i className="overview-dot blue" />
          Imports
        </span>
        <span>
          <i className="overview-dot teal" />
          Exports
        </span>
      </div>
    </div>
  );
}

export function StatusChart({ statuses }: { statuses: Dashboard['statuses'] }) {
  const total = statuses.reduce((sum, item) => sum + item.count, 0);
  const parts = statuses.filter((item) => item.count > 0);
  return (
    <div className="overview-status-body">
      <div className="overview-donut">
        <svg
          viewBox="0 0 180 180"
          role="img"
          aria-label={`Répartition des ${total} relevés par statut`}
        >
          <circle cx="90" cy="90" r="70" fill="none" stroke="var(--surface-alt)" strokeWidth="19" />
          {statuses
            .filter((item) => item.count > 0)
            .map((item, index) => {
              const length = (item.count / total) * 100;
              const start =
                (parts.slice(0, index).reduce((sum, previous) => sum + previous.count, 0) / total) *
                100;
              return (
                <circle
                  key={item.status}
                  cx="90"
                  cy="90"
                  r="70"
                  pathLength={100}
                  fill="none"
                  stroke={statusInfo(item.status).color}
                  strokeWidth="19"
                  strokeDasharray={`${Math.max(0, length - (length < 100 ? Math.min(1.3, length / 4) : 0))} 100`}
                  strokeDashoffset={-start}
                  transform="rotate(-90 90 90)"
                >
                  <title>
                    {statusInfo(item.status).label} : {item.count}
                  </title>
                </circle>
              );
            })}
        </svg>
        <div>
          <strong>{total.toLocaleString('fr-FR')}</strong>
          <span>relevés</span>
        </div>
      </div>
      <ul className="overview-status-list">
        {statuses.map((item) => (
          <li key={item.status}>
            <span>
              <i className="overview-dot" style={{ background: statusInfo(item.status).color }} />
              {statusInfo(item.status).label}
            </span>
            <strong>{item.count.toLocaleString('fr-FR')}</strong>
            <small>{total ? Math.round((item.count / total) * 100) : 0} %</small>
          </li>
        ))}
      </ul>
      {!total && (
        <p className="overview-empty-note">La répartition apparaîtra après votre premier import.</p>
      )}
    </div>
  );
}
