import { fireEvent, screen } from '@testing-library/react'
import type { UserEvent } from '@testing-library/user-event'

export async function escolherOpcao(
  user: UserEvent,
  nomeDoCampo: string,
  textoDaOpcao: string,
): Promise<void> {
  await user.click(screen.getByRole('combobox', { name: nomeDoCampo }))
  await user.click(await screen.findByRole('option', { name: textoDaOpcao }))
}

/** Campo de data (`input type="date"`): o userEvent.type não foi medido nele. */
export function preencherData(rotulo: string, valor: string): void {
  fireEvent.change(screen.getByLabelText(rotulo), { target: { value: valor } })
}
