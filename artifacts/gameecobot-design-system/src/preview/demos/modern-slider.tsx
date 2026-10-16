import { useState } from 'react';
import { ModernCard } from '../../components/ui/ModernCard';
import { ModernSlider } from '../../components/ui/ModernSlider';

export function ModernSliderDemo() {
  const [angle, setAngle] = useState(76);

  return (
    <div className="grid gap-5 lg:grid-cols-2">
      <ModernCard
        eyebrow="Angle snapping"
        title="Réglage de précision"
        description="Curseur accessible au clavier et ajustable par glissement."
      >
        <ModernSlider
          label="Intensité"
          max={100}
          min={1}
          onValueChange={setAngle}
          value={angle}
        />
      </ModernCard>
      <ModernCard eyebrow="État" title="Valeur actuelle">
        <p className="font-mono text-5xl font-medium tabular-nums text-primary">
          {angle}
          <span className="ml-1 text-xl text-muted-foreground">%</span>
        </p>
        <div className="mt-5 h-px bg-border/70" />
        <p className="mt-4 text-sm text-muted-foreground">
          {angle >= 80 ? 'Réglage élevé' : angle >= 40 ? 'Réglage équilibré' : 'Réglage léger'}
        </p>
      </ModernCard>
    </div>
  );
}
