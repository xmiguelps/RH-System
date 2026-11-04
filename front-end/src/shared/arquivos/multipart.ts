type ValorDoCampo = string | number | boolean | File | null | undefined

/** Corpo multipart/form-data para o openapi-fetch (o tipo gerado para arquivo é string). */
export function multipart<B>(campos: Record<string, ValorDoCampo>): {
  body: B
  bodySerializer: (corpo: B) => FormData
} {
  return {
    body: campos as unknown as B,
    bodySerializer: (corpo) => {
      const formulario = new FormData()
      for (const [nome, valor] of Object.entries(corpo as Record<string, ValorDoCampo>)) {
        if (valor === null || valor === undefined) continue
        formulario.append(nome, valor instanceof File ? valor : String(valor))
      }
      return formulario
    },
  }
}
