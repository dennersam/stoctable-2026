import { Link } from 'react-router-dom';
import { ArrowRight, Check } from 'lucide-react';
import { Button } from '@/components/ui/Button';
import { HeroScene } from './HeroScene';

const destaques = ['Sem instalação', 'Multi-filial', 'Seus dados isolados'];

export function Hero() {
  return (
    <section>
      <div className="mx-auto grid max-w-6xl items-center gap-10 px-4 pt-10 pb-16 sm:px-6 lg:grid-cols-[1.15fr_1fr] lg:gap-16 lg:py-24">
        <div>
          <h1 className="font-condensed text-5xl font-extrabold leading-[1.02] tracking-tight sm:text-6xl lg:text-7xl">
            Todo o seu balcão em um sistema só
          </h1>

          <p className="mt-5 max-w-lg text-lg leading-relaxed text-gray-600 dark:text-gray-300">
            Gestão para lojas de peças e acessórios: orçamento, caixa e estoque
            conversando entre si, em todas as suas lojas. O vendedor monta o orçamento, o caixa recebe pronto e o
            estoque baixa sozinho, sem planilha no meio do caminho.
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
                Criar conta
                <ArrowRight size={18} />
              </Link>
            </Button>
            <Button asChild variant="outline" size="lg">
              <a href="#como-funciona">Ver como funciona</a>
            </Button>
          </div>
        </div>

        <HeroScene />
      </div>
    </section>
  );
}
