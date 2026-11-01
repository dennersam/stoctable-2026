import { Component, type ErrorInfo, type ReactNode } from 'react';
import { Button } from '@/components/ui/Button';

interface Props {
  children: ReactNode;
}

interface State {
  failed: boolean;
}

/**
 * Sem isto, um erro de renderização desmonta a árvore inteira e deixa a tela
 * branca, sem caminho de volta. Dentro do Layout, envolve só a página: o menu
 * continua de pé e a pessoa consegue sair dali.
 */
export class ErrorBoundary extends Component<Props, State> {
  state: State = { failed: false };

  static getDerivedStateFromError(): State {
    return { failed: true };
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error('Erro de renderização', error, info.componentStack);
  }

  render() {
    if (!this.state.failed) return this.props.children;

    return (
      <div role="alert" className="mx-auto max-w-md px-4 py-16 text-center">
        <h1 className="text-lg font-semibold text-gray-900 dark:text-white">Esta tela não abriu</h1>
        <p className="mt-2 text-sm text-gray-600 dark:text-gray-300">
          Recarregue a página. Se continuar, avise o suporte dizendo o que estava fazendo.
        </p>
        <Button className="mt-5" onClick={() => window.location.reload()}>Recarregar a página</Button>
      </div>
    );
  }
}
