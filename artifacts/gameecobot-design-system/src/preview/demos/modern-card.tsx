import { Cpu, Gauge, MemoryStick, Zap } from 'lucide-react';
import { ModernCard } from '../../components/ui/ModernCard';

const metrics = [
  { label: 'Processeur', value: '24%', Icon: Cpu },
  { label: 'Mémoire', value: '68%', Icon: MemoryStick },
  { label: 'Applications', value: '08', Icon: Gauge },
];

export function ModernCardDemo() {
  return (
    <div className="grid gap-4 xl:grid-cols-[1.2fr_1fr]">
      {metrics.map(({ label, value, Icon }) => (
        <ModernCard
          key={label}
          className="min-h-44"
          eyebrow="Surveillance système"
          title={label}
          trailing={
            <span className="grid size-10 place-items-center rounded-xl border border-primary/20 bg-primary/10 text-primary">
              <Icon size={18} />
            </span>
          }
        >
          <p className="font-mono text-4xl font-medium tracking-tight">
            {value}
          </p>
          <p className="mt-2 text-xs text-muted-foreground">
            Mesure locale de démonstration
          </p>
        </ModernCard>
      ))}
      <ModernCard
        className="xl:col-span-2"
        eyebrow="État du système"
        title="Mode Éco prêt"
        trailing={
          <span className="grid size-11 place-items-center rounded-full border border-primary/30 bg-primary/15 text-primary shadow-[0_0_24px_hsl(var(--primary)/0.22)]">
            <Zap size={18} />
          </span>
        }
      >
        <div className="flex flex-wrap items-center justify-between gap-4">
          <p className="max-w-xl text-sm leading-relaxed text-muted-foreground">
            Une surface de tableau de bord compacte qui distingue nettement
            l’état, la mesure et l’action.
          </p>
          <div className="h-2 w-40 overflow-hidden rounded-full bg-muted">
            <div className="h-full w-3/4 rounded-full bg-gradient-to-r from-primary to-accent" />
          </div>
        </div>
      </ModernCard>
    </div>
  );
}
