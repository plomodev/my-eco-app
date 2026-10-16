import { Minus, Power, X } from 'lucide-react';
import { ModernCard } from '../../components/ui/ModernCard';
import { ModernCircleButton } from '../../components/ui/ModernCircleButton';

export function ModernCircleButtonDemo() {
  return (
    <div className="grid gap-5 md:grid-cols-2">
      <ModernCard eyebrow="Contrôles de fenêtre" title="Actions compactes">
        <div className="flex items-center gap-4">
          <ModernCircleButton label="Réduire la fenêtre">
            <Minus size={17} />
          </ModernCircleButton>
          <ModernCircleButton label="Fermer la fenêtre" destructive>
            <X size={17} />
          </ModernCircleButton>
          <ModernCircleButton label="Démarrer le mode éco" size="lg">
            <Power size={18} />
          </ModernCircleButton>
          <ModernCircleButton label="Action indisponible" disabled>
            <Power size={16} />
          </ModernCircleButton>
        </div>
      </ModernCard>
      <ModernCard eyebrow="Contraste" title="Taille compacte">
        <div className="flex items-center gap-4">
          <ModernCircleButton label="Petit contrôle" size="sm">
            <Minus size={14} />
          </ModernCircleButton>
          <ModernCircleButton label="Contrôle standard">
            <Power size={16} />
          </ModernCircleButton>
          <span className="text-sm text-muted-foreground">
            Libellé accessible obligatoire
          </span>
        </div>
      </ModernCard>
    </div>
  );
}
