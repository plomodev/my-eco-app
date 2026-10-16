import { HardwareGraphControl } from '../../components/ui/HardwareGraphControl';
import { ModernCard } from '../../components/ui/ModernCard';

export function HardwareGraphControlDemo() {
  return (
    <div className="space-y-5">
      <HardwareGraphControl />
      <ModernCard
        eyebrow="État des sources"
        title="Valeurs d’exemple"
        description="Le graphique illustre la présentation et les changements de ressource. Il ne lit pas les capteurs de l’ordinateur depuis cette prévisualisation."
      >
        <p className="text-sm leading-relaxed text-muted-foreground">
          Les vues CPU et RAM affichent des séries fictives. La vue GPU reste
          indisponible, comme dans le code Windows actuel, jusqu’à l’ajout d’un
          fournisseur de télémétrie.
        </p>
      </ModernCard>
    </div>
  );
}
