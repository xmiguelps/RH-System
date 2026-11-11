type Renovar = () => Promise<string | null>

let renovarRegistrado: Renovar | null = null

// O AuthProvider registra aqui a renovação dele; o cliente HTTP a chama quando uma chamada recebe 401.
export const renovadorDeAcesso = {
  definir(renovar: Renovar | null): void {
    renovarRegistrado = renovar
  },
  renovar(): Promise<string | null> {
    return renovarRegistrado ? renovarRegistrado() : Promise.resolve(null)
  },
}
