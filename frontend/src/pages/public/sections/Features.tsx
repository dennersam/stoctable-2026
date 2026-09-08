import {
  Boxes,
  FileText,
  Receipt,
  Store,
  Users,
  ShieldCheck,
  type LucideIcon,
} from 'lucide-react';
import { Card, CardTitle, CardDescription } from '@/components/ui/Card';

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
          <h2 className="text-3xl font-bold tracking-tight sm:text-4xl">
            O que o Stoctable faz
          </h2>
          <p className="mt-4 text-lg text-gray-600 dark:text-gray-300">
            Cada parte do fluxo de uma loja de peças, sem retrabalho entre elas.
          </p>
        </div>

        <div className="mt-12 grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
          {features.map(({ icon: Icon, title, description }) => (
            <Card key={title}>
              <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-brand-100 text-brand-700 dark:bg-brand-900 dark:text-brand-200">
                <Icon size={20} />
              </div>
              <CardTitle className="mt-4">{title}</CardTitle>
              <CardDescription>{description}</CardDescription>
            </Card>
          ))}
        </div>
      </div>
    </section>
  );
}
