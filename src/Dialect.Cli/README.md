# Dialect CLI

Ferramenta de linha de comando para descobrir, traduzir e gerar relatórios sobre SQL embutido em código C#.

Este README explica rapidamente como construir e usar a CLI localizada em `src/Dialect.Cli`.

## Requisitos

- .NET SDK 8.0+ (recomenda-se usar `net10.0`/SDK mais recente disponível).
- Código-fonte do projeto (este repositório).

## Build rápido

A partir da raiz do repositório:

```bash
dotnet build Dialect.sln -c Debug
```

ou para release:

```bash
dotnet build Dialect.sln -c Release
```

## Executando a CLI (a partir do código)

Use `dotnet run` apontando para o projeto `src/Dialect.Cli` e passe o comando desejado após `--`.

Exemplo básico:

```bash
dotnet run -p src/Dialect.Cli -- translate-files -s ./src -t PostgreSql -o ./reports -v
```

Também é possível executar o artefato compilado diretamente (após `dotnet build`):

```bash
dotnet src/Dialect.Cli/bin/Debug/net10.0/Dialect.Cli.dll translate-files -s ./src -t MySql -o ./reports
```

## Comando principal: `translate-files`

Este é o comando usado pelos testes e pela automação para descobrir strings SQL em arquivos C# e gerar relatórios e/ou reescrever trechos detectados.

Opções principais:

- `--source-dir`, `-s <path>`: Diretório contendo os arquivos C# (obrigatório).
- `--output-dir`, `-o <path>`: Diretório de saída para relatórios (padrão: `./reports`).
- `--from`, `-f <dialect>`: Dialeto de origem (`SqlServer|PostgreSql|MySql`). Padrão: auto-detect.
- `--to`, `-t <dialect>`: Dialeto alvo (`SqlServer|PostgreSql|MySql`). (obrigatório para traduções).
- `--patterns`, `-p <patterns>`: Padrões de arquivo, separados por vírgula (padrão: `*.cs`).
- `--formats <formats>`: Formatos de relatório: `json`, `md`, `html` (padrão: todos).
- `--no-backup`: Não criar backup antes de modificar arquivos.
- `--dry-run`: Executa validações e gera relatórios sem modificar arquivos.
- `--verbose`, `-v`: Habilita logs mais verbosos.
- `--help`: Exibe ajuda do comando.

Exemplo de uso com `dry-run`:

```bash
dotnet run -p src/Dialect.Cli -- translate-files -s ./samples/Dialect.Samples -t MySql --dry-run -v
```

## Saída e relatórios

A CLI gera relatórios em `--output-dir` (por padrão `./reports`). Tipicamente gerará arquivos como:

- `report.json`
- `report.md`
- `report.html`

Os relatórios incluem pares SQL original/convertido (quando aplicável) e metadados (arquivo, linha, confiança heurística). Arquivos temporários e logs auxiliares podem ser gravados em `%TEMP%` durante execuções de teste.

## Segurança e validações

A CLI executa validações básicas de segurança no caminho de origem (por exemplo, proteção contra path traversal e verificação de permissão de leitura). Se você ver mensagens do tipo:

```
Source directory failed security validation (path traversal or inaccessible)
```

verifique se o caminho existe, é acessível e não aponta para fora do workspace esperado.

## Dicas de depuração

- Use `-v` para obter logs mais detalhados.
- Para problemas de desempenho, execuções repetidas se beneficiam de um "warm-up" do analisador Roslyn presente no serviço de descoberta; rode a CLI duas vezes para comparar tempos.
- Se precisar inspecionar um caso específico, rode `translate-files` com `--dry-run` e examine o `report.json` gerado.

## Exemplos práticos

1) Gerar apenas HTML e MD (sem reescrita):

```bash
dotnet run -p src/Dialect.Cli -- translate-files -s ./src -t PostgreSql --formats md,html -o ./reports
```

2) Reescrever arquivos (com backup) de `SqlServer` para `MySql`:

```bash
dotnet run -p src/Dialect.Cli -- translate-files -s ./src -f SqlServer -t MySql -o ./reports
```

3) Executar contra um único diretório de amostra (modo verboso):

```bash
dotnet run -p src/Dialect.Cli -- translate-files -s ./samples/Dialect.Samples -t PostgreSql -v
```

## Contribuindo

- Abra uma issue ou PR na raíz do repositório com problemas ou melhorias.
- Para adicionar novos comandos, verifique a pasta [src/Dialect.Cli/Commands](src/Dialect.Cli/Commands) e siga os padrões existentes.

---

Se quiser, posso também:

- adicionar exemplos mais específicos (ex.: argumentos reais usados nos seus testes);
- publicar a CLI (`dotnet publish`) e adicionar instruções de instalação local.

