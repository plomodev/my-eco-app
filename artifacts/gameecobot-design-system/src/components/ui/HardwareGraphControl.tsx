import { useEffect, useId, useState } from 'react';
import { Cpu, MemoryStick, Monitor } from 'lucide-react';
import { ModernCard } from './ModernCard';

export type HardwareMetric = 'CPU' | 'RAM' | 'GPU';

export type HardwareGraphControlProps = {
  title?: string;
  metric?: HardwareMetric;
  defaultMetric?: HardwareMetric;
  onMetricChange?: (metric: HardwareMetric) => void;
};

const DEMO_SAMPLES: Record<HardwareMetric, number[]> = {
  CPU: [24, 22, 28, 32, 30, 25, 20, 18, 24, 35, 32, 28, 26, 31, 43, 38, 35, 30, 27, 29, 24, 24, 25, 24],
  RAM: [62, 62, 63, 63, 64, 64, 64, 65, 65, 66, 66, 67, 67, 68, 68, 68, 68, 68, 67, 68, 68, 68, 68, 68],
  GPU: [],
};

const METRICS: Array<{
  id: HardwareMetric;
  label: string;
  Icon: typeof Cpu;
  color: string;
}> = [
  { id: 'CPU', label: 'Processeur', Icon: Cpu, color: 'hsl(var(--chart-1))' },
  { id: 'RAM', label: 'RAM', Icon: MemoryStick, color: 'hsl(var(--chart-2))' },
  { id: 'GPU', label: 'GPU', Icon: Monitor, color: 'hsl(var(--chart-3))' },
];

const CHART = {
  left: 44,
  right: 886,
  top: 22,
  bottom: 205,
  width: 842,
  height: 183,
};

function makeLinePath(values: number[]) {
  const points = values.map((value, index) => {
    const x = CHART.left + (CHART.width * index) / Math.max(values.length - 1, 1);
    const y = CHART.bottom - (value / 100) * CHART.height;
    return { x, y };
  });

  const line = points
    .map((point, index) => `${index === 0 ? 'M' : 'L'} ${point.x} ${point.y}`)
    .join(' ');
  const first = points[0];
  const last = points[points.length - 1];
  const area = `${line} L ${last.x} ${CHART.bottom} L ${first.x} ${CHART.bottom} Z`;

  return { line, area };
}

export function HardwareGraphControl({
  title = 'Historique des ressources',
  metric,
  defaultMetric = 'CPU',
  onMetricChange,
}: HardwareGraphControlProps) {
  const [uncontrolledMetric, setUncontrolledMetric] = useState(defaultMetric);
  const [animationProgress, setAnimationProgress] = useState(0);
  const gradientId = useId().replaceAll(':', '');
  const activeMetric = metric ?? uncontrolledMetric;
  const selected = METRICS.find((item) => item.id === activeMetric) ?? METRICS[0];
  const samples = DEMO_SAMPLES[activeMetric];
  const currentValue = samples.length > 0 ? samples[samples.length - 1] : null;
  const path = samples.length > 0 ? makeLinePath(samples) : null;

  useEffect(() => {
    setAnimationProgress(0);
    if (DEMO_SAMPLES[activeMetric].length === 0) return;

    let frameId = 0;
    let startTime = 0;
    const draw = (now: number) => {
      if (startTime === 0) startTime = now;
      const progress = Math.min((now - startTime) / 1100, 1);
      setAnimationProgress(progress);
      if (progress < 1) frameId = requestAnimationFrame(draw);
    };

    frameId = requestAnimationFrame(draw);
    return () => cancelAnimationFrame(frameId);
  }, [activeMetric]);

  const selectMetric = (nextMetric: HardwareMetric) => {
    if (metric === undefined) {
      setUncontrolledMetric(nextMetric);
    }
    onMetricChange?.(nextMetric);
  };

  return (
    <ModernCard
      className="p-4 sm:p-5"
      description="Historique illustratif de 24 mesures à une seconde d’intervalle."
      eyebrow="Moniteur de performances · exemple"
      title={title}
      trailing={
        <span className="rounded-full border border-primary/20 bg-primary/10 px-2.5 py-1 font-mono text-[10px] font-medium uppercase tracking-[0.12em] text-accent">
          Aperçu
        </span>
      }
    >
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div aria-label="Choisir une ressource" className="flex flex-wrap gap-2" role="group">
          {METRICS.map(({ id, label, Icon }) => (
            <button
              aria-pressed={activeMetric === id}
              className="inline-flex min-h-9 items-center gap-2 rounded-lg border border-border bg-background/60 px-3 text-xs font-medium text-muted-foreground transition-colors hover:border-primary/40 hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring aria-pressed:border-primary/45 aria-pressed:bg-primary/15 aria-pressed:text-foreground"
              key={id}
              onClick={() => selectMetric(id)}
              type="button"
            >
              <Icon size={14} />
              {label}
            </button>
          ))}
        </div>
        <span
          aria-live="polite"
          className="inline-flex items-center gap-2 text-xs text-muted-foreground"
        >
          <span
            aria-hidden="true"
            className="size-2 rounded-full"
            style={{ backgroundColor: selected.color }}
          />
          {currentValue === null ? 'N/D' : `${currentValue}%`}
          {currentValue === null ? '' : ' · exemple'}
        </span>
      </div>

      <div className="mt-4 rounded-xl border border-border/80 bg-background/55 px-2 pb-2 pt-4 sm:px-4">
        <svg
          aria-label={`${selected.label} : ${currentValue === null ? 'mesure GPU indisponible' : `${currentValue}% dans cette illustration`}`}
          className="h-48 w-full overflow-visible sm:h-56"
          preserveAspectRatio="none"
          role="img"
          viewBox="0 0 900 244"
        >
          <defs>
            <linearGradient id={gradientId} x1="0" x2="0" y1="0" y2="1">
              <stop offset="0%" stopColor={selected.color} stopOpacity="0.3" />
              <stop offset="100%" stopColor={selected.color} stopOpacity="0.015" />
            </linearGradient>
          </defs>
          {[0, 25, 50, 75, 100].map((value) => {
            const y = CHART.bottom - (value / 100) * CHART.height;
            return (
              <g key={value}>
                <line
                  stroke="hsl(var(--border) / 0.65)"
                  strokeDasharray={value === 0 ? undefined : '3 6'}
                  strokeWidth="1"
                  x1={CHART.left}
                  x2={CHART.right}
                  y1={y}
                  y2={y}
                />
                <text
                  fill="hsl(var(--muted-foreground))"
                  fontSize="10"
                  textAnchor="end"
                  x={CHART.left - 10}
                  y={y + 3}
                >
                  {value}%
                </text>
              </g>
            );
          })}
          {[0, 1, 2, 3, 4].map((index) => {
            const x = CHART.left + (CHART.width * index) / 4;
            return (
              <line
                key={index}
                stroke="hsl(var(--border) / 0.32)"
                strokeDasharray="2 7"
                strokeWidth="1"
                x1={x}
                x2={x}
                y1={CHART.top}
                y2={CHART.bottom}
              />
            );
          })}
          {path ? (
            <>
              <path
                d={path.area}
                fill={`url(#${gradientId})`}
                style={{ opacity: animationProgress }}
              />
              <path
                d={path.line}
                fill="none"
                stroke={selected.color}
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth="3"
                pathLength="1000"
                strokeDasharray="1000"
                strokeDashoffset={1000 * (1 - animationProgress)}
                vectorEffect="non-scaling-stroke"
              />
              <circle
                cx={CHART.right}
                cy={
                  CHART.bottom -
                  ((currentValue ?? 0) / 100) * CHART.height
                }
                fill={selected.color}
                r="4.5"
                stroke="hsl(var(--card))"
                strokeWidth="2"
                className="motion-safe:animate-pulse motion-reduce:animate-none"
                vectorEffect="non-scaling-stroke"
              />
            </>
          ) : (
            <text
              fill="hsl(var(--muted-foreground))"
              fontSize="13"
              textAnchor="middle"
              x="465"
              y="117"
            >
              La télémétrie GPU n’est pas configurée dans l’application source
            </text>
          )}
        </svg>
        <div className="ml-11 mr-1 mt-1 flex justify-between font-mono text-[10px] text-muted-foreground">
          <span>−24 s</span>
          <span>−18 s</span>
          <span>−12 s</span>
          <span>−6 s</span>
          <span>Maintenant</span>
        </div>
      </div>
    </ModernCard>
  );
}
