import { useState } from 'react';
import { ModernCard } from '../../components/ui/ModernCard';
import { ModernToggleSwitch } from '../../components/ui/ModernToggleSwitch';

export function ModernToggleSwitchDemo() {
  const [autoDetect, setAutoDetect] = useState(true);
  const [mouseDelay, setMouseDelay] = useState(false);

  return (
    <div className="grid gap-5 lg:grid-cols-2">
      <ModernCard eyebrow="Préférences" title="Bascule et état">
        <div className="space-y-5">
          <ModernToggleSwitch
            checked={autoDetect}
            description="Détecte le jeu actif au premier plan."
            label="Détection automatique"
            onCheckedChange={setAutoDetect}
          />
          <div className="h-px bg-border/70" />
          <ModernToggleSwitch
            checked={mouseDelay}
            description="Contrôle désactivé à titre d’exemple."
            disabled
            label="Optimisation de la souris"
          />
        </div>
      </ModernCard>
      <ModernCard eyebrow="Réglage" title="Mode sans délai">
        <ModernToggleSwitch
          checked={mouseDelay}
          description="Modifie l’état dès l’activation."
          label="Sans délai"
          onCheckedChange={setMouseDelay}
        />
        <p aria-live="polite" className="mt-5 rounded-lg border border-border/70 bg-muted/50 px-3 py-2 text-xs text-muted-foreground">
          État : {mouseDelay ? 'activé' : 'désactivé'}
        </p>
      </ModernCard>
    </div>
  );
}
