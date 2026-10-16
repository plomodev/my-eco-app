import type { ReactNode } from 'react';
import {
  Activity,
  Cpu,
  Gamepad2,
  MemoryStick,
  Monitor,
  Minus,
  Power,
  Settings2,
  Sparkles,
  Zap,
} from 'lucide-react';
import { ModernButton } from '../components/ui/ModernButton';
import { ModernCard } from '../components/ui/ModernCard';
import { ModernCircleButton } from '../components/ui/ModernCircleButton';
import { HardwareGraphControl } from '../components/ui/HardwareGraphControl';
import { ModernSlider } from '../components/ui/ModernSlider';

const COLOR_ROLES = [
  ['background', 'bg-background'],
  ['foreground', 'bg-foreground'],
  ['border', 'bg-border'],
  ['card', 'bg-card'],
  ['cardForeground', 'bg-card-foreground'],
  ['popover', 'bg-popover'],
  ['popoverForeground', 'bg-popover-foreground'],
  ['primary', 'bg-primary'],
  ['primaryForeground', 'bg-primary-foreground'],
  ['secondary', 'bg-secondary'],
  ['secondaryForeground', 'bg-secondary-foreground'],
  ['muted', 'bg-muted'],
  ['mutedForeground', 'bg-muted-foreground'],
  ['accent', 'bg-accent'],
  ['accentForeground', 'bg-accent-foreground'],
  ['destructive', 'bg-destructive'],
  ['destructiveForeground', 'bg-destructive-foreground'],
  ['input', 'bg-input'],
  ['ring', 'bg-ring'],
  ['chart1', 'bg-chart-1'],
  ['chart2', 'bg-chart-2'],
  ['chart3', 'bg-chart-3'],
  ['chart4', 'bg-chart-4'],
  ['chart5', 'bg-chart-5'],
  ['sidebar', 'bg-sidebar'],
  ['sidebarForeground', 'bg-sidebar-foreground'],
  ['sidebarBorder', 'bg-sidebar-border'],
  ['sidebarPrimary', 'bg-sidebar-primary'],
  ['sidebarPrimaryForeground', 'bg-sidebar-primary-foreground'],
  ['sidebarAccent', 'bg-sidebar-accent'],
  ['sidebarAccentForeground', 'bg-sidebar-accent-foreground'],
  ['sidebarRing', 'bg-sidebar-ring'],
] as const;

const SOURCE_COLORS = [
  { name: 'Violet principal', value: '#7C3AED' },
  { name: 'Violet accent', value: '#A855F7' },
  { name: 'Violet clair', value: '#C084FC' },
  { name: 'Lueur', value: '#D8B4FE' },
];

export function OverviewPage() {
  return (
    <div className="space-y-7">
      <section
        aria-labelledby="task-manager-heading"
        className="flex flex-wrap items-end justify-between gap-4"
      >
        <div>
          <p className="text-xs font-medium uppercase tracking-[0.18em] text-accent">
            Gestionnaire des tâches
          </p>
          <h2
            className="mt-2 text-2xl font-medium tracking-tight sm:text-3xl"
            id="task-manager-heading"
          >
            Surveillance du PC
          </h2>
          <p className="mt-2 max-w-2xl text-sm text-muted-foreground">
            Processeur, mémoire, carte graphique et jeux installés localement.
          </p>
        </div>
        <span className="rounded-full border border-border bg-card/75 px-3 py-1.5 text-[10px] font-medium uppercase tracking-[0.15em] text-muted-foreground">
          Aperçu visuel
        </span>
      </section>

      <section
        aria-label="Composants du PC"
        className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3"
      >
        <MetricCard
          icon={<Cpu size={17} />}
          label="Processeur · CPU"
          value="24%"
          trend="Exemple d’utilisation · nom du modèle à détecter"
          utilization={24}
        />
        <MetricCard
          icon={<MemoryStick size={17} />}
          label="Mémoire · RAM"
          value="68%"
          trend="Exemple d’utilisation · capacité à détecter"
          utilization={68}
        />
        <MetricCard
          icon={<Monitor size={17} />}
          label="Carte graphique · GPU"
          value="N/D"
          trend="Télémétrie GPU absente dans la version actuelle."
          unavailable
        />
      </section>

      <HardwareGraphControl title="Évolution des performances" />

      <ModernCard
        eyebrow="Jeux de cet ordinateur"
        title="Jeux installés sur ce PC"
        description="La liste doit contenir uniquement les jeux trouvés sur cet ordinateur, pas un catalogue général."
        trailing={
          <span className="rounded-full border border-border bg-muted/70 px-2.5 py-1 text-[10px] font-medium uppercase tracking-[0.14em] text-muted-foreground">
            Scan en attente
          </span>
        }
      >
        <div className="flex flex-col items-start gap-4 rounded-xl border border-dashed border-border bg-background/45 p-5 sm:flex-row sm:items-center sm:justify-between">
          <div className="flex items-start gap-3">
            <span className="grid size-10 shrink-0 place-items-center rounded-xl border border-primary/20 bg-primary/10 text-primary">
              <Gamepad2 size={18} />
            </span>
            <div>
              <p className="text-sm font-medium">Aucun scan local lancé</p>
              <p className="mt-1 max-w-xl text-xs leading-relaxed text-muted-foreground">
                La prévisualisation ne peut pas lire les fichiers de ton PC.
                Dans l’application Windows, le scan affichera seulement les
                jeux installés détectés localement.
              </p>
            </div>
          </div>
          <ModernButton disabled variant="secondary">
            Scanner depuis Windows
          </ModernButton>
        </div>
      </ModernCard>

      <div className="rounded-xl border border-primary/20 bg-primary/5 px-4 py-3 text-xs leading-relaxed text-muted-foreground">
        Les chiffres CPU/RAM ci-dessus sont des exemples de maquette, pas des
        mesures de ton PC. Le GPU reste indisponible tant que l’application
        Windows ne possède pas de fournisseur de télémétrie.
      </div>

      <section className="grid gap-4 lg:grid-cols-[1.1fr_0.9fr]">
        <ModernCard
          eyebrow="Réglage"
          title="Intensité d’optimisation"
          trailing={
            <span className="grid size-9 place-items-center rounded-xl border border-primary/25 bg-primary/10 text-primary">
              <Activity size={17} />
            </span>
          }
        >
          <ModernSlider
            defaultValue={76}
            label="Niveau"
            max={100}
            min={1}
          />
          <div className="mt-5 flex items-center gap-2 text-xs text-muted-foreground">
            <Sparkles size={14} className="text-accent" />
            Réglage visuel uniquement dans cette prévisualisation.
          </div>
        </ModernCard>
        <ModernCard
          eyebrow="Commandes"
          title="Actions de gestion"
          trailing={
            <ModernCircleButton label="Ouvrir les réglages" size="sm">
              <Settings2 size={15} />
            </ModernCircleButton>
          }
        >
          <div className="flex flex-wrap items-center gap-3">
            <ModernButton disabled variant="secondary">
              Activer le Mode Éco
            </ModernButton>
            <ModernCircleButton label="Réduire la fenêtre">
              <Minus size={15} />
            </ModernCircleButton>
            <ModernCircleButton label="Action système indisponible" disabled>
              <Power size={15} />
            </ModernCircleButton>
          </div>
          <p className="mt-4 text-xs text-muted-foreground">
            Les actions de l’application réelle seront reliées après validation
            du visuel.
          </p>
        </ModernCard>
      </section>
    </div>
  );
}

function MetricCard({
  icon,
  label,
  value,
  trend,
  utilization,
  unavailable = false,
}: {
  icon: ReactNode;
  label: string;
  value: string;
  trend: string;
  utilization?: number;
  unavailable?: boolean;
}) {
  return (
    <ModernCard
      className="min-h-36"
      eyebrow="Mesure système"
      title={label}
      trailing={
        <span className="grid size-9 place-items-center rounded-xl border border-primary/20 bg-primary/10 text-primary">
          {icon}
        </span>
      }
    >
      <div className="flex items-baseline gap-3">
        <span className={`font-mono text-3xl font-medium tabular-nums ${unavailable ? 'text-muted-foreground' : 'text-foreground'}`}>
          {value}
        </span>
        <span className="text-[10px] uppercase tracking-[0.12em] text-muted-foreground">
          {unavailable ? 'indisponible' : 'exemple'}
        </span>
      </div>
      <div
        aria-hidden="true"
        className="mt-3 h-1.5 overflow-hidden rounded-full bg-muted"
      >
        {utilization !== undefined ? (
          <div
            className="h-full rounded-full bg-gradient-to-r from-primary to-accent"
            style={{ width: `${utilization}%` }}
          />
        ) : null}
      </div>
      <p className="mt-2 text-xs text-muted-foreground">{trend}</p>
    </ModernCard>
  );
}

export function BrandLogoPage() {
  return (
    <div className="grid gap-5 md:grid-cols-[1fr_1.2fr]">
      <div className="ec-surface grid min-h-72 place-items-center p-8">
        <img
          alt="Icône originale de GameEcoBot"
          className="size-48 rounded-[2.5rem] object-contain shadow-[0_18px_50px_hsl(var(--primary)/0.28)]"
          src={`${import.meta.env.BASE_URL}gameecobot-mark.png`}
        />
      </div>
      <ModernCard
        eyebrow="Fichier de marque source"
        title="Icône de l’application"
        description="Icône extraite du dépôt GameEcoBot et conservée sans modification."
      >
        <div className="space-y-4 text-sm leading-relaxed text-muted-foreground">
          <p>
            La prévisualisation utilise la version PNG du plus grand format
            présent dans l’icône Windows d’origine.
          </p>
          <div className="rounded-lg border border-border bg-muted/40 px-4 py-3 font-mono text-xs text-foreground">
            app.ico · aperçu 256 × 256
          </div>
          <p className="text-xs">
            La source ne comprend pas de règles écrites sur l’espace libre,
            l’usage sur fond clair ou les variantes du logo.
          </p>
        </div>
      </ModernCard>
    </div>
  );
}

export function ColorsPage() {
  return (
    <div className="space-y-8">
      <section>
        <SectionHeading
          title="Violets de la source"
          description="Valeurs visibles dans le code WinForms d’origine."
        />
        <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
          {SOURCE_COLORS.map((color) => (
            <div
              className="ec-surface flex items-center gap-3 p-4"
              key={color.value}
            >
              <span
                aria-hidden="true"
                className="size-10 shrink-0 rounded-xl border border-foreground/15 shadow-[0_5px_12px_hsl(var(--background)/0.3)]"
                style={{ background: color.value }}
              />
              <span className="min-w-0">
                <span className="block text-sm font-medium">{color.name}</span>
                <span className="mt-1 block font-mono text-xs text-muted-foreground">
                  {color.value}
                </span>
              </span>
            </div>
          ))}
        </div>
      </section>

      <section>
        <SectionHeading
          title="Rôles des couleurs"
          description="Valeurs actives selon le thème choisi dans l’en-tête."
        />
        <div className="grid grid-cols-2 gap-2 sm:grid-cols-3 lg:grid-cols-4">
          {COLOR_ROLES.map(([name, swatch]) => (
            <div
              className="flex items-center gap-3 rounded-xl border border-border/70 bg-card/55 p-3"
              key={name}
            >
              <span
                aria-hidden="true"
                className={`size-7 shrink-0 rounded-lg border border-foreground/10 shadow-sm ${swatch}`}
              />
              <span className="truncate font-mono text-[11px] text-muted-foreground">
                {name}
              </span>
            </div>
          ))}
        </div>
        <p className="mt-4 max-w-3xl text-xs leading-relaxed text-muted-foreground">
          Le dépôt ne définit qu’un thème sombre. Les valeurs claires sont une
          adaptation de ce système, pas une palette observée dans la source.
        </p>
      </section>
    </div>
  );
}

export function FontsPage() {
  return (
    <div className="space-y-5">
      <ModernCard
        eyebrow="Typographie d’interface"
        title="Ash"
        description="Police principale fournie avec le système visuel, avec Segoe UI et une police sans sérif comme solutions de repli."
      >
        <div className="space-y-4 pt-2">
          <p className="text-4xl font-medium tracking-tight">
            Mesures système
          </p>
          <p className="text-lg text-muted-foreground">
            Réglages simples, états faciles à comprendre.
          </p>
          <p className="text-sm leading-relaxed text-muted-foreground">
            Les textes restent en graisse normale. Les titres et les valeurs
            gardent leur hiérarchie grâce à la taille, au contraste et à
            l’espacement.
          </p>
        </div>
      </ModernCard>
      <ModernCard
        eyebrow="Données"
        title="Mono"
        description="Cascadia Code et Consolas gardent des chiffres à largeur constante pour les mesures."
      >
        <p className="font-mono text-3xl tabular-nums">24.8%</p>
      </ModernCard>
    </div>
  );
}

export function LayoutPage() {
  return (
    <div className="space-y-6">
      <ModernCard
        eyebrow="Rythme"
        title="Espacement de base · 4 px"
        description="Rythme compact adapté au tableau de bord de bureau source."
      >
        <div className="flex flex-wrap items-end gap-4">
          {[1, 2, 3, 4, 6, 8].map((unit) => (
            <div className="text-center" key={unit}>
              <div
                className="mx-auto rounded-md bg-gradient-to-br from-primary to-accent"
                style={{ height: `${unit * 4}px`, width: `${unit * 4}px` }}
              />
              <p className="mt-2 font-mono text-[10px] text-muted-foreground">
                {unit * 4}px
              </p>
            </div>
          ))}
        </div>
      </ModernCard>
      <div className="grid gap-4 md:grid-cols-2">
        <ModernCard eyebrow="Rayon source" title="12 px">
          <div className="rounded-xl border border-primary/45 bg-gradient-to-br from-card to-secondary/70 p-5 shadow-[0_14px_30px_hsl(var(--primary)/0.18),inset_0_1px_0_hsl(var(--foreground)/0.1)]">
            <p className="text-sm font-medium">Surface arrondie</p>
            <p className="mt-1 text-xs text-muted-foreground">
              Contour fin, ombre violette et reflet discret.
            </p>
          </div>
        </ModernCard>
        <ModernCard eyebrow="Profondeur" title="Élévation retenue">
          <div className="flex items-center gap-4">
            <span className="grid size-12 place-items-center rounded-full border border-primary/35 bg-gradient-to-br from-accent/70 to-primary text-primary-foreground shadow-[0_10px_20px_hsl(var(--primary)/0.32),inset_0_1px_0_hsl(var(--foreground)/0.26)]">
              <Sparkles size={18} />
            </span>
            <p className="max-w-xs text-sm leading-relaxed text-muted-foreground">
              Les ombres renforcent la hiérarchie, sans transformer l’écran en
              scène décorative.
            </p>
          </div>
        </ModernCard>
      </div>
    </div>
  );
}

export function MotionPage() {
  return (
    <div className="space-y-6">
      <ModernCard
        eyebrow="États interactifs"
        title="Survol, pression et focus"
        description="Survole et active les éléments pour voir le déplacement, le bord lumineux et l’effet de pression."
      >
        <div className="flex flex-wrap items-center gap-5 py-3">
          <ModernButton>Survoler ou activer</ModernButton>
          <ModernCircleButton label="Action énergie">
            <Zap size={17} />
          </ModernCircleButton>
          <ModernCircleButton label="Action avec erreur" destructive>
            <Power size={16} />
          </ModernCircleButton>
          <span className="rounded-full border border-primary/25 bg-primary/10 px-3 py-1.5 text-xs text-accent">
            Relief léger · pression nette
          </span>
        </div>
      </ModernCard>
      <div className="grid gap-4 md:grid-cols-2">
        <ModernCard eyebrow="Surfaces" title="Lueur de bord">
          <p className="text-sm leading-relaxed text-muted-foreground">
            La carte se soulève de quelques pixels et prend un léger angle au
            survol; le contour violet indique la zone active.
          </p>
        </ModernCard>
        <ModernCard eyebrow="Accessibilité" title="Mouvement réduit">
          <p className="text-sm leading-relaxed text-muted-foreground">
            Les transitions restent courtes et sont supprimées lorsque
            l’appareil demande une réduction des animations.
          </p>
        </ModernCard>
      </div>
    </div>
  );
}

function SectionHeading({
  title,
  description,
}: {
  title: string;
  description: string;
}) {
  return (
    <div className="mb-4">
      <h2 className="text-lg font-medium tracking-tight">{title}</h2>
      <p className="mt-1 text-sm text-muted-foreground">{description}</p>
    </div>
  );
}
