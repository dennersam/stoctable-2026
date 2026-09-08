import { Link } from 'react-router-dom';
import { ArrowRight, Check } from 'lucide-react';
import { Button } from '@/components/ui/Button';
import { Badge } from '@/components/ui/Badge';
import heroImage from '@/assets/hero.png';

const destaques = ['Sem instalação', 'Multi-filial', 'Seus dados isolados'];

export function Hero() {
  return (
    <section className="relative overflow-hidden">
      {/* Brilho de fundo puramente decorativo — escondido de leitores de tela. */}
      <div
        aria-hidden
        className="pointer-events-none absolute -top-40 left-1/2 h-[32rem] w-[64rem] -translate-x-1/2 rounded-full bg-brand-400/20 blur-3xl dark:bg-brand-600/25"
      />

      <div className="relative mx-auto grid max-w-6xl items-center gap-12 px-4 py-20 sm:px-6 lg:grid-cols-2 lg:py-28">
        <div>
          <Badge>Gestão para lojas de peças e acessórios</Badge>

          <h1 className="mt-5 text-4xl font-bold leading-tight tracking-tight sm:text-5xl">
            Todo o seu balcão em{' '}
            <span className="text-brand-600 dark:text-brand-300">um sistema só</span>
          </h1>

          <p className="mt-5 max-w-lg text-lg leading-relaxed text-gray-600 dark:text-gray-300">
            Orçamento, caixa e estoque conversando entre si, em todas as suas
            lojas. O vendedor monta o orçamento, o caixa recebe pronto e o
            estoque baixa sozinho — sem planilha no meio do caminho.
          </p>

          <ul className="mt-6 flex flex-wrap gap-x-6 gap-y-2">
            {destaques.map((item) => (
              <li key={item} className="flex items-center gap-2 text-sm text-gray-700 dark:text-gray-300">
                <Check size={16} className="text-brand-600 dark:text-brand-300" />
                {item}
              </li>
            ))}
          </ul>

          <div className="mt-8 flex flex-col gap-3 sm:flex-row">
            <Button asChild size="lg">
              <Link to="/cadastro">
                Criar minha conta
                <ArrowRight size={18} />
              </Link>
            </Button>
            <Button asChild variant="outline" size="lg">
              <a href="#como-funciona">Ver como funciona</a>
            </Button>
          </div>
        </div>

        <div className="relative">
          <img
            src={heroImage}
            alt="Tela do Stoctable mostrando o painel de vendas"
            className="w-full rounded-xl border border-gray-200 shadow-2xl dark:border-white/10"
            loading="eager"
          />
        </div>
      </div>
    </section>
  );
}
