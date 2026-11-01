import {
  Boxes,
  FileText,
  Receipt,
  Store,
  Users,
  ShieldCheck,
  type LucideIcon,
} from 'lucide-react';

interface Feature {
  icon: LucideIcon;
  title: string;
  description: string;
}

const features: Feature[] = [
  {
    icon: FileText,
    title: 'Orçamentos',
    description:
      'O vendedor monta o orçamento no balcão e reserva o estoque na hora. Nada de vender duas vezes a mesma peça.',
  },
  {
    icon: Receipt,
    title: 'Caixa',
    description:
      'O orçamento chega pronto no caixa. Recebimento em várias formas de pagamento, com baixa de estoque automática.',
  },
  {
    icon: Boxes,
    title: 'Estoque',
    description:
      'Saldo por loja, movimentações registradas e alerta de estoque mínimo. Cada entrada e saída fica rastreada.',
  },
  {
    icon: Store,
    title: 'Multi-filial',
    description:
      'Catálogo, clientes e fornecedores compartilhados entre as lojas; estoque e caixa separados por filial.',
  },
  {
    icon: Users,
    title: 'Clientes e perfis',
    description:
      'Cadastro com histórico de compras e desconto por tipo de cliente — varejo, atacado, oficina ou revenda.',
  },
  {
    icon: ShieldCheck,
    title: 'Controle de acesso',
    description:
      'Perfis de administrador, atendente e caixa. Toda alteração fica registrada em auditoria, com autor e horário.',
  },
];

export function Features() {
  return (
    <section id="recursos" className="border-t border-gray-200 py-20 dark:border-white/10 lg:py-24">
      <div className="mx-auto max-w-6xl px-4 sm:px-6">
        <div className="max-w-2xl">
          <h2 className="font-condensed text-4xl font-bold tracking-tight sm:text-5xl">
            O que o Stoctable faz
          </h2>
          <p className="mt-4 text-lg text-gray-600 dark:text-gray-300">
            Cada parte do fluxo de uma loja de peças, sem retrabalho entre elas.
          </p>
        </div>

        <dl className="mt-12 grid gap-x-12 gap-y-10 sm:grid-cols-2 lg:grid-cols-3">
          {features.map(({ icon: Icon, title, description }) => (
            <div key={title}>
              <dt className="flex items-center gap-2.5 text-lg font-semibold">
                <Icon size={20} className="shrink-0 text-brand-600 dark:text-brand-300" aria-hidden="true" />
                {title}
              </dt>
              <dd className="mt-2 text-sm leading-relaxed text-gray-600 dark:text-gray-300">{description}</dd>
            </div>
          ))}
        </dl>
      </div>
    </section>
  );
}
