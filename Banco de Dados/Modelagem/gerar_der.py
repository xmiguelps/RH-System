#!/usr/bin/env python3
"""Gera o DER/MER do banco (Mermaid) a partir dos scripts `Banco de Dados/Scripts/V*.sql`, sem conectar em banco nenhum.

Uso (da raiz do repositório):  python3 "Banco de Dados/Modelagem/gerar_der.py"
Escreve `Banco de Dados/Modelagem/DER_gerado.md`. O resultado é determinístico: tabelas em ordem alfabética, colunas na ordem do script.
Lê só os `CREATE TABLE` e as chaves estrangeiras (`FOREIGN KEY ... REFERENCES`, dentro do `CREATE TABLE` ou em `ALTER TABLE ... ADD CONSTRAINT`).
O teste `DerGeradoTests` confere que o arquivo escrito está em dia com os scripts: mudou o esquema, rode este script e versione o resultado.
"""
import re
import sys
from pathlib import Path

RAIZ = Path(__file__).resolve().parents[2]
SCRIPTS = RAIZ / "Banco de Dados" / "Scripts"
SAIDA = RAIZ / "Banco de Dados" / "Modelagem" / "DER_gerado.md"

PALAVRAS_DE_REGRA = ("CONSTRAINT", "PRIMARY", "UNIQUE", "FOREIGN", "KEY", "INDEX", "CHECK")


def sem_comentarios(sql: str) -> str:
    sql = re.sub(r"/\*.*?\*/", "", sql, flags=re.S)
    return "\n".join(re.sub(r"--.*$", "", linha) for linha in sql.splitlines())


def corpo_da_tabela(sql: str, inicio: int) -> str:
    """O texto entre o parêntese que abre o CREATE TABLE e o que o fecha (contando parênteses)."""
    abre = sql.index("(", inicio)
    nivel = 0
    for i in range(abre, len(sql)):
        if sql[i] == "(":
            nivel += 1
        elif sql[i] == ")":
            nivel -= 1
            if nivel == 0:
                return sql[abre + 1 : i]
    raise ValueError("CREATE TABLE sem fechamento")


def partir_na_virgula(corpo: str) -> list[str]:
    itens, atual, nivel = [], [], 0
    for c in corpo:
        if c == "(":
            nivel += 1
        elif c == ")":
            nivel -= 1
        if c == "," and nivel == 0:
            itens.append("".join(atual).strip())
            atual = []
        else:
            atual.append(c)
    if "".join(atual).strip():
        itens.append("".join(atual).strip())
    return itens


def tipo_simples(texto: str) -> str:
    nome = re.match(r"[A-Za-z]+", texto)
    return (nome.group(0) if nome else "TEXT").upper()


def ler_esquema(sql: str):
    tabelas: dict[str, dict] = {}
    for m in re.finditer(r"CREATE TABLE\s+(?:IF NOT EXISTS\s+)?`?(\w+)`?", sql, flags=re.I):
        nome = m.group(1).upper()
        colunas, pks, fks = [], set(), []
        for item in partir_na_virgula(corpo_da_tabela(sql, m.end())):
            primeira = item.split(None, 1)[0].upper() if item else ""
            if primeira in PALAVRAS_DE_REGRA:
                pk = re.search(r"PRIMARY\s+KEY\s*\(([^)]*)\)", item, flags=re.I)
                if pk:
                    pks.update(c.strip().strip("`").upper() for c in pk.group(1).split(","))
                fk = re.search(r"FOREIGN\s+KEY\s*\(([^)]*)\)\s*REFERENCES\s+`?(\w+)`?\s*\(([^)]*)\)", item, flags=re.I)
                if fk:
                    fks.append((fk.group(1).strip().strip("`").upper(), fk.group(2).upper(), fk.group(3).strip().strip("`").upper()))
                continue
            partes = item.split(None, 1)
            coluna = partes[0].strip("`").upper()
            resto = partes[1] if len(partes) > 1 else ""
            if re.search(r"PRIMARY\s+KEY", resto, flags=re.I):
                pks.add(coluna)
            colunas.append((coluna, tipo_simples(resto)))
        tabelas[nome] = {"colunas": colunas, "pks": pks, "fks": fks}
    for m in re.finditer(r"ALTER TABLE\s+`?(\w+)`?\s+ADD\s+CONSTRAINT\s+\w+\s+FOREIGN\s+KEY\s*\(([^)]*)\)\s*REFERENCES\s+`?(\w+)`?\s*\(([^)]*)\)", sql, flags=re.I):
        tabelas[m.group(1).upper()]["fks"].append((m.group(2).strip().strip("`").upper(), m.group(3).upper(), m.group(4).strip().strip("`").upper()))
    return tabelas


def gerar() -> str:
    sql = "\n".join(sem_comentarios(p.read_text(encoding="utf-8")) for p in sorted(SCRIPTS.glob("V*.sql")))
    tabelas = ler_esquema(sql)
    linhas = [
        "# DER/MER gerado dos scripts",
        "",
        "Gerado por `Banco de Dados/Modelagem/gerar_der.py` a partir de `Banco de Dados/Scripts/V*.sql` (não edite à mão: mudou o esquema, rode o script de novo). "
        f"São **{len(tabelas)} tabelas** e **{sum(len(t['fks']) for t in tabelas.values())} chaves estrangeiras**. O desenho do grupo continua em `DER.png` e `MER_RHSystem.drawio`; este arquivo é o mesmo modelo em texto, versionável e sempre igual aos scripts.",
        "Abra no GitHub, no VS Code (extensão Mermaid) ou em <https://mermaid.live>. Cada relação diz a coluna que liga o filho ao pai.",
        "",
        "```mermaid",
        "erDiagram",
    ]
    for nome in sorted(tabelas):
        t = tabelas[nome]
        fk_colunas = {c.strip() for cols, _, _ in t["fks"] for c in cols.split(",")}  # chave composta: cada coluna recebe a marca FK
        linhas.append(f"    {nome} {{")
        for coluna, tipo in t["colunas"]:
            marca = " PK" if coluna in t["pks"] else (" FK" if coluna in fk_colunas else "")
            linhas.append(f"        {tipo} {coluna}{marca}")
        linhas.append("    }")
    for nome in sorted(tabelas):
        for coluna, pai, _ in sorted(set(tabelas[nome]["fks"])):
            linhas.append(f'    {pai} ||--o{{ {nome} : "{coluna}"')
    linhas.append("```")
    linhas.append("")
    return "\n".join(linhas)


if __name__ == "__main__":
    texto = gerar()
    if "--conferir" in sys.argv:
        atual = SAIDA.read_text(encoding="utf-8").replace("\r\n", "\n") if SAIDA.exists() else ""
        sys.exit(0 if atual == texto else 1)
    SAIDA.write_text(texto, encoding="utf-8", newline="\n")
    print(f"ok: {SAIDA}")
