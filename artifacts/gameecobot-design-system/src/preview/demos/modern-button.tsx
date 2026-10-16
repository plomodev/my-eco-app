import { useState } from 'react';
import { Search, Zap } from 'lucide-react';
import { ModernButton } from '../../components/ui/ModernButton';

export function ModernButtonDemo() {
  const [ecoEnabled, setEcoEnabled] = useState(false);
  const [scanCount, setScanCount] = useState(0);

  return (
    <div className="space-y-8">
      <section className="space-y-3">
        <p className="text-xs font-medium uppercase tracking-[0.18em] text-muted-foreground">
          Variantes
        </p>
        <div className="flex flex-wrap items-center gap-4">
          <ModernButton leadingIcon={<Zap size={16} />} onClick={() => setEcoEnabled(!ecoEnabled)}>
            {ecoEnabled ? 'Mode Éco activé' : 'Activer le Mode Éco'}
          </ModernButton>
          <ModernButton variant="secondary">Action secondaire</ModernButton>
          <ModernButton variant="ghost">Action discrète</ModernButton>
          <ModernButton variant="danger">Arrêter la session</ModernButton>
        </div>
      </section>
      <section className="space-y-3">
        <p className="text-xs font-medium uppercase tracking-[0.18em] text-muted-foreground">
          Tailles et états
        </p>
        <div className="flex flex-wrap items-center gap-4">
          <ModernButton size="sm">Compact</ModernButton>
          <ModernButton size="md">Standard</ModernButton>
          <ModernButton size="lg">Grand</ModernButton>
          <ModernButton disabled>Indisponible</ModernButton>
          <ModernButton
            leadingIcon={<Search size={16} />}
            variant="secondary"
            onClick={() => setScanCount((count) => count + 1)}
          >
            Lancer un scan · {scanCount}
          </ModernButton>
        </div>
      </section>
    </div>
  );
}
